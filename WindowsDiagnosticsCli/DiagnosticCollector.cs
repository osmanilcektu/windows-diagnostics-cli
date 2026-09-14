using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WindowsDiagnosticsCli;

public static class DiagnosticCollector
{
    public static async Task<DiagnosticReport> CollectAsync(string command, CancellationToken cancellationToken = default)
    {
        var needSystem = command is "all" or "system" or "health";
        var needDisks = command is "all" or "disk" or "health";
        var needNetwork = command is "all" or "network" or "health";
        var needServices = command is "all" or "services";

        var system = needSystem ? CollectSystem() : null;
        var disks = needDisks ? CollectDisks() : Array.Empty<DiskSnapshot>();
        var network = needNetwork ? CollectNetwork() : Array.Empty<NetworkSnapshot>();
        var services = needServices ? await CollectServicesAsync(cancellationToken) : Array.Empty<ServiceSnapshot>();
        var health = command is "all" or "health" ? EvaluateHealth(system, disks, network) : null;

        return new DiagnosticReport(
            DateTimeOffset.UtcNow,
            command,
            system,
            disks,
            network,
            services,
            health);
    }

    public static SystemSnapshot CollectSystem()
    {
        var memory = GetMemoryStatus();

        return new SystemSnapshot(
            Environment.MachineName,
            Environment.UserName,
            RuntimeInformation.OSDescription,
            RuntimeInformation.OSArchitecture.ToString(),
            RuntimeInformation.ProcessArchitecture.ToString(),
            GetCpuName(),
            Environment.ProcessorCount,
            TimeSpan.FromMilliseconds(Environment.TickCount64),
            memory.TotalPhysical,
            memory.AvailablePhysical,
            memory.MemoryLoadPercent);
    }

    public static IReadOnlyList<DiskSnapshot> CollectDisks()
    {
        var result = new List<DiskSnapshot>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady)
            {
                continue;
            }

            var total = drive.TotalSize;
            var free = drive.AvailableFreeSpace;
            var usedPercent = total == 0 ? 0 : Math.Round((total - free) * 100d / total, 2);

            result.Add(new DiskSnapshot(
                drive.Name,
                drive.DriveType.ToString(),
                string.IsNullOrWhiteSpace(drive.DriveFormat) ? null : drive.DriveFormat,
                total,
                free,
                usedPercent));
        }

        return result;
    }

    public static IReadOnlyList<NetworkSnapshot> CollectNetwork()
    {
        var result = new List<NetworkSnapshot>();

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            IPInterfaceProperties? properties = null;
            try
            {
                properties = nic.GetIPProperties();
            }
            catch (NetworkInformationException)
            {
            }

            var ipv4 = properties?.UnicastAddresses
                .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(address => address.Address.ToString())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(address => address, StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? Array.Empty<string>();

            var gateways = properties?.GatewayAddresses
                .Select(gateway => gateway.Address)
                .Where(address => address.AddressFamily == AddressFamily.InterNetwork)
                .Select(address => address.ToString())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? Array.Empty<string>();

            var dns = properties?.DnsAddresses
                .Select(address => address.ToString())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? Array.Empty<string>();

            result.Add(new NetworkSnapshot(
                nic.Name,
                nic.NetworkInterfaceType.ToString(),
                nic.OperationalStatus.ToString(),
                nic.Speed > 0 ? nic.Speed / 1_000_000 : 0,
                ipv4,
                gateways,
                dns));
        }

        return result
            .OrderByDescending(item => item.Status.Equals("Up", StringComparison.OrdinalIgnoreCase))
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static async Task<IReadOnlyList<ServiceSnapshot>> CollectServicesAsync(CancellationToken cancellationToken = default)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = "query type= service state= all",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Unable to start sc.exe.");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"sc.exe failed with exit code {process.ExitCode}: {stderr.Trim()}");
        }

        return ParseServiceQueryOutput(stdout);
    }

    public static IReadOnlyList<ServiceSnapshot> ParseServiceQueryOutput(string output)
    {
        var services = new List<ServiceSnapshot>();
        string? currentName = null;

        foreach (var rawLine in output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            var line = rawLine.Trim();

            if (line.StartsWith("SERVICE_NAME:", StringComparison.OrdinalIgnoreCase))
            {
                currentName = line[(line.IndexOf(':') + 1)..].Trim();
                continue;
            }

            if (currentName is null || !line.StartsWith("STATE", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var colonIndex = line.IndexOf(':');
            if (colonIndex < 0)
            {
                continue;
            }

            var payload = line[(colonIndex + 1)..].Trim();
            var parts = payload.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !int.TryParse(parts[0], out var stateCode))
            {
                continue;
            }

            services.Add(new ServiceSnapshot(currentName, stateCode, parts[1]));
            currentName = null;
        }

        return services.OrderBy(service => service.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static HealthSnapshot EvaluateHealth(
        SystemSnapshot? system,
        IReadOnlyList<DiskSnapshot> disks,
        IReadOnlyList<NetworkSnapshot> network)
    {
        var findings = new List<HealthFinding>();

        if (system is not null)
        {
            if (system.MemoryLoadPercent >= 95)
            {
                findings.Add(new HealthFinding(HealthLevel.Critical, "memory.critical", $"Physical memory load is {system.MemoryLoadPercent}%"));
            }
            else if (system.MemoryLoadPercent >= 85)
            {
                findings.Add(new HealthFinding(HealthLevel.Warning, "memory.high", $"Physical memory load is {system.MemoryLoadPercent}%"));
            }
        }

        foreach (var disk in disks)
        {
            var freePercent = disk.TotalBytes == 0 ? 100d : disk.FreeBytes * 100d / disk.TotalBytes;
            if (freePercent < 5)
            {
                findings.Add(new HealthFinding(HealthLevel.Critical, "disk.critical", $"{disk.Name} has only {freePercent:F1}% free space"));
            }
            else if (freePercent < 10)
            {
                findings.Add(new HealthFinding(HealthLevel.Warning, "disk.low", $"{disk.Name} has only {freePercent:F1}% free space"));
            }
        }

        if (network.Count > 0 && network.All(adapter => !adapter.Status.Equals("Up", StringComparison.OrdinalIgnoreCase)))
        {
            findings.Add(new HealthFinding(HealthLevel.Warning, "network.offline", "No network adapter is currently operational."));
        }

        if (findings.Count == 0)
        {
            findings.Add(new HealthFinding(HealthLevel.Healthy, "health.ok", "No critical system, disk, memory, or network condition was detected."));
        }

        var overall = findings.Any(finding => finding.Level == HealthLevel.Critical)
            ? HealthLevel.Critical
            : findings.Any(finding => finding.Level == HealthLevel.Warning)
                ? HealthLevel.Warning
                : HealthLevel.Healthy;

        return new HealthSnapshot(overall, findings);
    }

    private static string? GetCpuName()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            return key?.GetValue("ProcessorNameString")?.ToString()?.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static MemoryStatus GetMemoryStatus()
    {
        var status = new MemoryStatusEx();
        if (!GlobalMemoryStatusEx(status))
        {
            return new MemoryStatus(0, 0, 0);
        }

        return new MemoryStatus(status.TotalPhysical, status.AvailablePhysical, status.MemoryLoad);
    }

    private readonly record struct MemoryStatus(ulong TotalPhysical, ulong AvailablePhysical, uint MemoryLoadPercent);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class MemoryStatusEx
    {
        public uint Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx buffer);
}

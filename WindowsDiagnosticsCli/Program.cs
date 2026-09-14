using System.Text.Json;

namespace WindowsDiagnosticsCli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var options = CliOptions.Parse(args);

            if (options.ShowHelp)
            {
                PrintHelp();
                return 0;
            }

            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("windows-diagnostics-cli currently supports Windows only.");
                return 2;
            }

            var report = await DiagnosticCollector.CollectAsync(options.Command);
            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (!string.IsNullOrWhiteSpace(options.OutputPath))
            {
                var outputPath = Path.GetFullPath(options.OutputPath);
                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllTextAsync(outputPath, json);
                Console.WriteLine($"Report written to: {outputPath}");
            }

            if (options.Json)
            {
                Console.WriteLine(json);
            }
            else
            {
                PrintTextReport(report);
            }

            return report.Health?.Overall == HealthLevel.Critical ? 1 : 0;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine("Use --help to see available commands and options.");
            return 2;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Operation cancelled.");
            return 130;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Diagnostics failed: {ex.Message}");
            return 1;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Windows Diagnostics CLI");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  WindowsDiagnosticsCli [command] [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  health     Run a concise health assessment (default)");
        Console.WriteLine("  system     Show OS, CPU, memory, architecture, and uptime");
        Console.WriteLine("  disk       Show ready drives and free-space usage");
        Console.WriteLine("  network    Show network adapters, IPv4, gateways, and DNS");
        Console.WriteLine("  services   Show Windows service states using sc.exe");
        Console.WriteLine("  all        Collect every available section");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --json             Write the report as JSON to stdout");
        Console.WriteLine("  --output <path>    Also save the full JSON report to a file");
        Console.WriteLine("  -h, --help         Show this help text");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  WindowsDiagnosticsCli health");
        Console.WriteLine("  WindowsDiagnosticsCli all --json");
        Console.WriteLine("  WindowsDiagnosticsCli health --output report.json");
    }

    private static void PrintTextReport(DiagnosticReport report)
    {
        Console.WriteLine($"Windows Diagnostics CLI - {report.Command}");
        Console.WriteLine($"Generated: {report.GeneratedAt:O}");

        if (report.System is not null)
        {
            var system = report.System;
            Console.WriteLine();
            Console.WriteLine("[System]");
            Console.WriteLine($"Machine: {system.MachineName}");
            Console.WriteLine($"OS: {system.OperatingSystem} ({system.OsArchitecture})");
            Console.WriteLine($"CPU: {system.CpuName ?? "Unknown"}");
            Console.WriteLine($"Logical processors: {system.LogicalProcessors}");
            Console.WriteLine($"Memory: {FormatBytes(system.AvailableMemoryBytes)} available / {FormatBytes(system.TotalMemoryBytes)} total ({system.MemoryLoadPercent}% used)");
            Console.WriteLine($"Uptime: {FormatDuration(system.Uptime)}");
        }

        if (report.Disks.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("[Disks]");
            foreach (var disk in report.Disks)
            {
                Console.WriteLine($"{disk.Name,-8} {disk.DriveType,-10} {disk.UsedPercent,6:F2}% used  {FormatBytes(disk.FreeBytes)} free / {FormatBytes(disk.TotalBytes)}");
            }
        }

        if (report.Network.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("[Network]");
            foreach (var adapter in report.Network)
            {
                var addresses = adapter.IPv4Addresses.Count == 0 ? "no IPv4" : string.Join(", ", adapter.IPv4Addresses);
                Console.WriteLine($"{adapter.Name}: {adapter.Status}, {adapter.InterfaceType}, {adapter.SpeedMbps} Mbps, {addresses}");
            }
        }

        if (report.Services.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("[Services]");
            foreach (var service in report.Services)
            {
                Console.WriteLine($"{service.Name,-45} {service.State}");
            }
        }

        if (report.Health is not null)
        {
            Console.WriteLine();
            Console.WriteLine($"[Health] {report.Health.Overall}");
            foreach (var finding in report.Health.Findings)
            {
                Console.WriteLine($"- {finding.Level}: {finding.Message}");
            }
        }
    }

    private static string FormatBytes(ulong bytes)
    {
        const double unit = 1024d;
        if (bytes < unit)
        {
            return $"{bytes} B";
        }

        var value = (double)bytes;
        var units = new[] { "KB", "MB", "GB", "TB", "PB" };
        var index = -1;
        do
        {
            value /= unit;
            index++;
        }
        while (value >= unit && index < units.Length - 1);

        return $"{value:F2} {units[index]}";
    }

    private static string FormatDuration(TimeSpan value)
        => $"{(int)value.TotalDays}d {value.Hours}h {value.Minutes}m";
}

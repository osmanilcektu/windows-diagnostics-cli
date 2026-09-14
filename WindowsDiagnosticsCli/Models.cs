using System.Text.Json.Serialization;

namespace WindowsDiagnosticsCli;

public sealed record DiagnosticReport(
    DateTimeOffset GeneratedAt,
    string Command,
    SystemSnapshot? System,
    IReadOnlyList<DiskSnapshot> Disks,
    IReadOnlyList<NetworkSnapshot> Network,
    IReadOnlyList<ServiceSnapshot> Services,
    HealthSnapshot? Health);

public sealed record SystemSnapshot(
    string MachineName,
    string UserName,
    string OperatingSystem,
    string OsArchitecture,
    string ProcessArchitecture,
    string? CpuName,
    int LogicalProcessors,
    TimeSpan Uptime,
    ulong TotalMemoryBytes,
    ulong AvailableMemoryBytes,
    uint MemoryLoadPercent);

public sealed record DiskSnapshot(
    string Name,
    string DriveType,
    string? FileSystem,
    long TotalBytes,
    long FreeBytes,
    double UsedPercent);

public sealed record NetworkSnapshot(
    string Name,
    string InterfaceType,
    string Status,
    long SpeedMbps,
    IReadOnlyList<string> IPv4Addresses,
    IReadOnlyList<string> Gateways,
    IReadOnlyList<string> DnsServers);

public sealed record ServiceSnapshot(
    string Name,
    int StateCode,
    string State);

public enum HealthLevel
{
    Healthy,
    Warning,
    Critical
}

public sealed record HealthFinding(
    HealthLevel Level,
    string Code,
    string Message);

public sealed record HealthSnapshot(
    HealthLevel Overall,
    IReadOnlyList<HealthFinding> Findings);

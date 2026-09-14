namespace WindowsDiagnosticsCli.Tests;

public class CliOptionsTests
{
    [Fact]
    public void ParseDefaultsToHealth()
    {
        var options = CliOptions.Parse(Array.Empty<string>());

        Assert.Equal("health", options.Command);
        Assert.False(options.Json);
        Assert.Null(options.OutputPath);
    }

    [Fact]
    public void ParseSupportsCommandJsonAndOutput()
    {
        var options = CliOptions.Parse(new[] { "all", "--json", "--output", "report.json" });

        Assert.Equal("all", options.Command);
        Assert.True(options.Json);
        Assert.Equal("report.json", options.OutputPath);
    }

    [Fact]
    public void ParseRejectsUnknownCommand()
    {
        var exception = Assert.Throws<ArgumentException>(() => CliOptions.Parse(new[] { "unknown" }));

        Assert.Contains("Unknown command", exception.Message);
    }
}

public class DiagnosticCollectorTests
{
    [Fact]
    public void ParseServiceQueryOutputReadsServiceState()
    {
        const string output = """
SERVICE_NAME: EventLog
        TYPE               : 30  WIN32
        STATE              : 4  RUNNING

SERVICE_NAME: Spooler
        TYPE               : 110  WIN32
        STATE              : 1  STOPPED
""";

        var services = DiagnosticCollector.ParseServiceQueryOutput(output);

        Assert.Equal(2, services.Count);
        Assert.Equal("EventLog", services[0].Name);
        Assert.Equal(4, services[0].StateCode);
        Assert.Equal("RUNNING", services[0].State);
        Assert.Equal("Spooler", services[1].Name);
        Assert.Equal("STOPPED", services[1].State);
    }

    [Fact]
    public void EvaluateHealthWarnsWhenDiskSpaceIsLow()
    {
        var disks = new[]
        {
            new DiskSnapshot("C:\\", "Fixed", "NTFS", 1000, 80, 92)
        };

        var health = DiagnosticCollector.EvaluateHealth(null, disks, Array.Empty<NetworkSnapshot>());

        Assert.Equal(HealthLevel.Warning, health.Overall);
        Assert.Contains(health.Findings, finding => finding.Code == "disk.low");
    }

    [Fact]
    public void EvaluateHealthIsHealthyWhenNoProblemsAreDetected()
    {
        var system = new SystemSnapshot(
            "PC",
            "user",
            "Windows",
            "X64",
            "X64",
            "CPU",
            8,
            TimeSpan.FromHours(2),
            16UL * 1024 * 1024 * 1024,
            8UL * 1024 * 1024 * 1024,
            50);

        var disks = new[]
        {
            new DiskSnapshot("C:\\", "Fixed", "NTFS", 1000, 500, 50)
        };

        var network = new[]
        {
            new NetworkSnapshot("Ethernet", "Ethernet", "Up", 1000, new[] { "192.0.2.10" }, Array.Empty<string>(), Array.Empty<string>())
        };

        var health = DiagnosticCollector.EvaluateHealth(system, disks, network);

        Assert.Equal(HealthLevel.Healthy, health.Overall);
        Assert.Single(health.Findings);
        Assert.Equal("health.ok", health.Findings[0].Code);
    }
}

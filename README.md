# Windows Diagnostics CLI

A lightweight, native .NET command-line tool for collecting Windows system, memory, disk, network, service, and health diagnostics.

The project is intentionally dependency-light and designed for quick local troubleshooting, repeatable JSON reports, automation, and support workflows.

## Features

- Windows, architecture, CPU, logical processor, uptime, and physical memory information
- Ready-drive capacity and free-space reporting
- Network adapter status, speed, IPv4 addresses, gateways, and DNS servers
- Windows service state collection through `sc.exe`
- Health assessment for high memory pressure, low disk space, and unavailable network adapters
- Human-readable console output
- Structured JSON output
- Optional report export to a file
- xUnit regression tests
- GitHub Actions CI on Windows

## Requirements

- Windows 10 or Windows 11
- .NET 10 SDK to build from source
- .NET 10 runtime to run framework-dependent builds

## Build

```powershell
git clone https://github.com/osmanilcektu/windows-diagnostics-cli.git
cd windows-diagnostics-cli
dotnet restore
dotnet build
dotnet test
```

## Usage

Run the default health assessment:

```powershell
dotnet run --project .\WindowsDiagnosticsCli
```

Collect all sections:

```powershell
dotnet run --project .\WindowsDiagnosticsCli -- all
```

Get JSON output:

```powershell
dotnet run --project .\WindowsDiagnosticsCli -- all --json
```

Save a JSON report:

```powershell
dotnet run --project .\WindowsDiagnosticsCli -- health --output report.json
```

Available commands:

| Command | Purpose |
| --- | --- |
| `health` | System health assessment; default command |
| `system` | OS, CPU, memory, architecture, and uptime |
| `disk` | Ready-drive capacity and free-space usage |
| `network` | Adapter state, IPv4, gateways, and DNS |
| `services` | Windows service states |
| `all` | Collect all available sections |

Options:

| Option | Purpose |
| --- | --- |
| `--json` | Print structured JSON to stdout |
| `--output <path>` | Save the complete JSON report to a file |
| `-h`, `--help` | Show help |

## Exit codes

- `0`: successful run; no critical health condition detected
- `1`: critical health condition or diagnostic failure
- `2`: invalid arguments or unsupported operating system
- `130`: operation cancelled

## Health thresholds

The initial health rules are intentionally simple and visible:

- Memory load `>= 85%`: warning
- Memory load `>= 95%`: critical
- Disk free space `< 10%`: warning
- Disk free space `< 5%`: critical
- No operational network adapter: warning

These thresholds are intended as troubleshooting signals, not a replacement for full monitoring or hardware diagnostics.

## Privacy

The tool runs locally. It does not upload diagnostic data. Reports can include machine name, local user name, network configuration, service names, storage information, and system details, so review exported JSON before sharing it publicly.

## Roadmap

- Configurable thresholds
- Optional event-log diagnostics
- Selected service checks
- Connectivity probes
- Release binaries for `win-x64` and `win-arm64`
- Signed release checksums

## Contributing

Issues and focused pull requests are welcome. Please include a clear reproduction or rationale and keep platform-specific behavior explicit.

## License

MIT

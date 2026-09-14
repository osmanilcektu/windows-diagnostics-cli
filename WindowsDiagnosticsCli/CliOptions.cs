namespace WindowsDiagnosticsCli;

public sealed record CliOptions(string Command, bool Json, string? OutputPath, bool ShowHelp)
{
    private static readonly HashSet<string> Commands = new(StringComparer.OrdinalIgnoreCase)
    {
        "all",
        "system",
        "network",
        "disk",
        "services",
        "health"
    };

    public static CliOptions Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new CliOptions("health", false, null, false);
        }

        var command = "health";
        var json = false;
        string? outputPath = null;
        var showHelp = false;
        var commandSeen = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg is "-h" or "--help")
            {
                showHelp = true;
                continue;
            }

            if (arg.Equals("--json", StringComparison.OrdinalIgnoreCase))
            {
                json = true;
                continue;
            }

            if (arg.Equals("--output", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]))
                {
                    throw new ArgumentException("--output requires a file path.");
                }

                outputPath = args[++i];
                continue;
            }

            if (arg.StartsWith('-'))
            {
                throw new ArgumentException($"Unknown option: {arg}");
            }

            if (commandSeen)
            {
                throw new ArgumentException($"Unexpected argument: {arg}");
            }

            if (!Commands.Contains(arg))
            {
                throw new ArgumentException($"Unknown command: {arg}");
            }

            command = arg.ToLowerInvariant();
            commandSeen = true;
        }

        return new CliOptions(command, json, outputPath, showHelp);
    }
}

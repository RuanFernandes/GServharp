namespace GServ.Network;

public sealed record DevOnlyLocalServerCommandLineConfig(
    bool Enabled,
    string RootPath,
    string LevelName,
    int Port);

public static class DevOnlyLocalServerCommandLine
{
    public static DevOnlyLocalServerCommandLineConfig Parse(IReadOnlyList<string> args)
    {
        var enabled = args.Contains("--dev-only-local", StringComparer.Ordinal);
        var root = ValueAfter(args, "--dev-root") ?? string.Empty;
        var level = ValueAfter(args, "--dev-level") ?? string.Empty;
        var portText = ValueAfter(args, "--port");
        var port = int.TryParse(portText, out var parsedPort) ? parsedPort : 14900;

        if (!enabled)
            return new DevOnlyLocalServerCommandLineConfig(false, root, level, port);

        if (string.IsNullOrWhiteSpace(root))
            throw new ArgumentException("--dev-root is required for --dev-only-local.");
        if (string.IsNullOrWhiteSpace(level))
            throw new ArgumentException("--dev-level is required for --dev-only-local.");

        return new DevOnlyLocalServerCommandLineConfig(true, root, level, port);
    }

    private static string? ValueAfter(IReadOnlyList<string> args, string key)
    {
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (string.Equals(args[i], key, StringComparison.Ordinal))
                return args[i + 1];
        }

        return null;
    }
}

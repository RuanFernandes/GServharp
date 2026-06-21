namespace Preagonal.GServer.Network;

public sealed record LocalDebugCommandLineConfig(
    bool Enabled,
    string RootPath,
    string LevelName,
    int Port);

public static class LocalDebugCommandLine
{
    public static LocalDebugCommandLineConfig Parse(IReadOnlyList<string> args)
    {
        var enabled =
            args.Contains("--local-debug", StringComparer.Ordinal) ||
            args.Contains("--dev-only-local", StringComparer.Ordinal);
        var root = ValueAfter(args, "--dev-root") ?? string.Empty;
        var level = ValueAfter(args, "--dev-level") ?? string.Empty;
        var portText = ValueAfter(args, "--port");
        var port = int.TryParse(portText, out var parsedPort) ? parsedPort : 14900;

        if (!enabled)
            return new LocalDebugCommandLineConfig(false, root, level, port);

        if (string.IsNullOrWhiteSpace(root))
            throw new ArgumentException("--dev-root is required for --local-debug.");
        if (string.IsNullOrWhiteSpace(level))
            throw new ArgumentException("--dev-level is required for --local-debug.");

        return new LocalDebugCommandLineConfig(true, root, level, port);
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

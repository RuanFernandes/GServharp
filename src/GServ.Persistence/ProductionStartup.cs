namespace GServ.Persistence;

public sealed record ProductionStartupOverrides(
    string? Server,
    string? Port,
    string? LocalIp,
    string? ServerIp,
    string? ServerInterface,
    string? StaffAccount,
    string? ServerName,
    bool ShowHelp)
{
    public static ProductionStartupOverrides Empty { get; } = new(null, null, null, null, null, null, null, false);
}

public enum ProductionStartupServerSource
{
    None,
    CommandLineOrEnvironment,
    StartupServerFile,
    SingleServersDirectory,
}

public sealed record ProductionStartupResolution(
    bool Success,
    string? ServerName,
    string? ServerPath,
    ProductionStartupServerSource Source,
    string? Diagnostic)
{
    public static ProductionStartupResolution Failed(string diagnostic) =>
        new(false, null, null, ProductionStartupServerSource.None, diagnostic);
}

public static class ProductionStartupCommandLine
{
    public static ProductionStartupOverrides Parse(IReadOnlyList<string> args, Func<string, string?> getEnvironmentVariable)
    {
        var useEnvironment = getEnvironmentVariable("USE_ENV") is not null;
        if (useEnvironment)
            return ParseEnvironment(getEnvironmentVariable);

        var result = MutableOverrides.Empty;
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                var key = arg[2..];
                if (key == "help")
                    return result.ToImmutable(showHelp: true);

                if (++i == args.Count)
                    return result.ToImmutable(showHelp: true);

                ApplyLongOption(ref result, key, args[i]);
            }
            else if (arg.Length > 0 && arg[0] == '-')
            {
                for (var j = 1; j < arg.Length; j++)
                {
                    switch (arg[j])
                    {
                        case 'h':
                            return result.ToImmutable(showHelp: true);
                        case 's':
                            if (++i == args.Count)
                                return result.ToImmutable(showHelp: true);
                            result.Server = args[i];
                            break;
                        case 'p':
                            if (string.IsNullOrEmpty(result.Server))
                                break;
                            if (++i == args.Count)
                                return result.ToImmutable(showHelp: true);
                            result.Port = args[i];
                            break;
                    }
                }
            }
        }

        return result.ToImmutable(showHelp: false);
    }

    private static ProductionStartupOverrides ParseEnvironment(Func<string, string?> getEnvironmentVariable)
    {
        var result = MutableOverrides.Empty;
        result.Server = getEnvironmentVariable("SERVER");
        if (string.IsNullOrEmpty(result.Server))
            return result.ToImmutable(showHelp: false);

        result.Port = getEnvironmentVariable("PORT");
        result.LocalIp = getEnvironmentVariable("LOCALIP");
        result.ServerIp = getEnvironmentVariable("SERVERIP");
        result.ServerInterface = getEnvironmentVariable("INTERFACE");
        result.StaffAccount = getEnvironmentVariable("STAFFACCOUNT");
        result.ServerName = getEnvironmentVariable("SERVERNAME");
        return result.ToImmutable(showHelp: false);
    }

    private static void ApplyLongOption(ref MutableOverrides result, string key, string value)
    {
        switch (key)
        {
            case "server":
                result.Server = value;
                break;
            case "port" when !string.IsNullOrEmpty(result.Server):
                result.Port = value;
                break;
            case "localip" when !string.IsNullOrEmpty(result.Server):
                result.LocalIp = value;
                break;
            case "serverip" when !string.IsNullOrEmpty(result.Server):
                result.ServerIp = value;
                break;
            case "interface" when !string.IsNullOrEmpty(result.Server):
                result.ServerInterface = value;
                break;
            case "staff" when !string.IsNullOrEmpty(result.Server):
                result.StaffAccount = value;
                break;
            case "name" when !string.IsNullOrEmpty(result.Server):
                result.ServerName = value;
                break;
        }
    }

    private struct MutableOverrides
    {
        public string? Server;
        public string? Port;
        public string? LocalIp;
        public string? ServerIp;
        public string? ServerInterface;
        public string? StaffAccount;
        public string? ServerName;

        public static MutableOverrides Empty => new();

        public readonly ProductionStartupOverrides ToImmutable(bool showHelp) =>
            new(Server, Port, LocalIp, ServerIp, ServerInterface, StaffAccount, ServerName, showHelp);
    }
}

public static class ProductionStartupResolver
{
    public static ProductionStartupResolution Resolve(string homePath, ProductionStartupOverrides overrides)
    {
        var normalizedHome = EnsureTrailingSeparator(Path.GetFullPath(homePath));
        if (!string.IsNullOrEmpty(overrides.Server))
            return Success(normalizedHome, overrides.Server, ProductionStartupServerSource.CommandLineOrEnvironment);

        var startupPath = Path.Combine(normalizedHome, "startupserver.txt");
        if (File.Exists(startupPath))
        {
            var startup = File.ReadAllText(startupPath);
            if (startup.Length > 0)
                return Success(normalizedHome, startup, ProductionStartupServerSource.StartupServerFile);
        }

        var serversPath = Path.Combine(normalizedHome, "servers");
        if (Directory.Exists(serversPath))
        {
            var servers = Directory.EnumerateDirectories(serversPath).Select(Path.GetFileName).ToArray();
            if (servers.Length == 1 && !string.IsNullOrEmpty(servers[0]))
                return Success(normalizedHome, servers[0]!, ProductionStartupServerSource.SingleServersDirectory);
        }

        return ProductionStartupResolution.Failed(
            "C++ startup would return ERR_SETTINGS because no override server, non-empty startupserver.txt, or single servers/ directory was found.");
    }

    public static string BuildServerPath(string homePath, string serverName) =>
        EnsureTrailingSeparator(Path.Combine(EnsureTrailingSeparator(Path.GetFullPath(homePath)), "servers", serverName));

    private static ProductionStartupResolution Success(string normalizedHome, string serverName, ProductionStartupServerSource source) =>
        new(true, serverName, BuildServerPath(normalizedHome, serverName), source, null);

    private static string EnsureTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
}

public sealed record ProductionStartupSnapshot(
    ProductionStartupResolution Resolution,
    Gs2Settings ServerOptions,
    Gs2Settings AdminConfig)
{
    public bool IsProductionRuntimeBlocked => true;
    public string BlockedReason =>
        "Production sockets/auth/gameplay are intentionally not started until later milestones port Server::init, ServerList, and gameplay runtime behavior.";
}

public static class ProductionStartupLoader
{
    public static ProductionStartupSnapshot Load(string homePath, ProductionStartupOverrides overrides)
    {
        var resolution = ProductionStartupResolver.Resolve(homePath, overrides);
        if (!resolution.Success || resolution.ServerPath is null)
            return new ProductionStartupSnapshot(resolution, Gs2Settings.Parse(""), Gs2Settings.Parse(""));

        var configPath = Path.Combine(resolution.ServerPath, "config");
        var serverOptions = Gs2Settings.LoadFile(Path.Combine(configPath, "serveroptions.txt"));
        var adminConfig = Gs2Settings.LoadFile(Path.Combine(configPath, "adminconfig.txt"));
        return new ProductionStartupSnapshot(resolution, serverOptions, adminConfig);
    }
}

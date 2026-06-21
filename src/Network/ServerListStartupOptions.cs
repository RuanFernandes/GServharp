using Preagonal.GServer.Persistence;

namespace Preagonal.GServer.Network;

public static class ServerListStartupOptions
{
    private const string AppVersion = "3.0.9-beta";

    public static ServerListConnectOptions FromStartupSnapshot(
        ServerStartupSnapshot snapshot,
        ServerStartupOverrides overrides)
    {
        if (!snapshot.Resolution.Success || string.IsNullOrEmpty(snapshot.Resolution.ServerName))
            throw new InvalidOperationException("Server startup must resolve a server before list-server registration.");

        var options = snapshot.ServerOptions;
        var admin = snapshot.AdminConfig;
        var serverName = string.IsNullOrEmpty(overrides.ServerName)
            ? options.GetString("name", snapshot.Resolution.ServerName)
            : overrides.ServerName;
        var serverPort = string.IsNullOrEmpty(overrides.Port)
            ? options.GetString("serverport", "14802")
            : overrides.Port;
        var serverIp = string.IsNullOrEmpty(overrides.ServerIp)
            ? options.GetString("serverip", "AUTO")
            : overrides.ServerIp;
        var localIp = string.IsNullOrEmpty(overrides.LocalIp)
            ? options.GetString("localip", "AUTO")
            : overrides.LocalIp;

        return new ServerListConnectOptions(
            ListIp: options.GetString("listip", "listserver.graal.in"),
            ListPort: options.GetString("listport", "14900"),
            Name: serverName,
            Description: options.GetString("description", ""),
            Language: options.GetString("language", "English"),
            Version: AppVersion,
            Url: options.GetString("url", "http://www.graal.in/"),
            ServerIp: serverIp,
            ServerPort: serverPort,
            LocalIp: localIp,
            HqPassword: admin.GetString("hq_password", ""),
            HqLevel: admin.GetInt("hq_level", 1),
            OnlyStaff: options.GetBool("onlystaff", false),
            AllowedVersions: options.Exists("allowedversions")
                ? SplitCsv(options.GetString("allowedversions", ""))
                : snapshot.EffectiveAllowedVersions);
    }

    private static IReadOnlyList<string> SplitCsv(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

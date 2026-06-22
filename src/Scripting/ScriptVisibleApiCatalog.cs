namespace Preagonal.GServer.Scripting;

public sealed record ScriptVisibleApiStatus(
    string Name,
    string SourceFile,
    bool IsImplemented,
    string Blocker);

public static class ScriptVisibleApiCatalog
{
    private const string RuntimeBlocker =
        "Pending GS2Engine binding coverage for the modular NPC-server runtime.";

    public static IReadOnlyList<ScriptVisibleApiStatus> All { get; } =
    [
        Implemented("echo", "GS2Engine"),
        Implemented("trace", "GS2Engine"),
        Implemented("printf", "GS2Engine"),
        Implemented("sendtonc", "GS2Engine"),
        Implemented("base64encode", "GS2Engine"),
        Implemented("base64decode", "GS2Engine"),
        Implemented("getimgwidth", "GS2Engine"),
        Implemented("getimgheight", "GS2Engine"),
        Implemented("triggerclient", "GS2Engine"),
        Implemented("findplayer", "GS2Engine"),
        Implemented("findPlayer", "GS2Engine"),
        Implemented("sendpm", "GS2Engine"),
        Implemented("sendPM", "GS2Engine"),
        Implemented("addweapon", "GS2Engine"),
        Implemented("addWeapon", "GS2Engine"),
        Implemented("removeweapon", "GS2Engine"),
        Implemented("removeWeapon", "GS2Engine"),
        Implemented("screenwidth", "GS2Engine"),
        Implemented("screenheight", "GS2Engine"),
        Implemented("TAB", "GS2Engine"),
        Implemented("NL", "GS2Engine"),
        Implemented("NULL", "GS2Engine"),
        Implemented("nil", "GS2Engine"),
        Implemented("name", "GS2Engine"),
        Implemented("params", "GS2Engine"),
        Implemented("server", "GS2Engine"),
        Implemented("serverr", "GS2Engine"),
        Implemented("serveroptions", "GS2Engine"),
        Blocked("global", "GS2Engine"),
        Blocked("environment", "GS2Engine"),
        Blocked("server.flags", "GS2Engine"),
        Blocked("player", "GS2Engine"),
        Blocked("player.attr", "GS2Engine"),
        Blocked("player.colors", "GS2Engine"),
        Blocked("player.flags", "GS2Engine"),
        Blocked("npc", "GS2Engine"),
        Blocked("npc.attr", "GS2Engine"),
        Blocked("npc.colors", "GS2Engine"),
        Blocked("npc.flags", "GS2Engine"),
        Blocked("npc.save", "GS2Engine"),
        Blocked("level", "GS2Engine"),
        Blocked("level.tiles", "GS2Engine"),
        Blocked("level.links", "GS2Engine"),
        Blocked("level.signs", "GS2Engine"),
        Blocked("level.chests", "GS2Engine"),
        Blocked("level.npcs", "GS2Engine"),
        Blocked("level.link", "GS2Engine"),
        Blocked("level.sign", "GS2Engine"),
        Blocked("level.chest", "GS2Engine"),
        Blocked("weapon", "GS2Engine")
    ];

    private static ScriptVisibleApiStatus Blocked(string name, string sourceFile) =>
        new(name, sourceFile, IsImplemented: false, RuntimeBlocker);

    private static ScriptVisibleApiStatus Implemented(string name, string sourceFile) =>
        new(name, sourceFile, IsImplemented: true, "");
}

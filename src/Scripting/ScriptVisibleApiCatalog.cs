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
        Blocked("global", "GS2Engine"),
        Blocked("environment", "GS2Engine"),
        Blocked("server", "GS2Engine"),
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

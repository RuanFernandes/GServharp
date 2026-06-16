namespace GServ.Scripting;

public sealed record ScriptVisibleApiStatus(
    string Name,
    string SourceFile,
    bool IsImplemented,
    string Blocker);

public static class ScriptVisibleApiCatalog
{
    private const string RuntimeBlocker =
        "Blocked until the exact V8NPCSERVER binding behavior is ported function-by-function from the original C++ source.";

    public static IReadOnlyList<ScriptVisibleApiStatus> All { get; } =
    [
        Blocked("global", "V8FunctionsImpl.cpp"),
        Blocked("environment", "V8EnvironmentImpl.cpp"),
        Blocked("server", "V8ServerImpl.cpp"),
        Blocked("server.flags", "V8ServerImpl.cpp"),
        Blocked("player", "V8PlayerImpl.cpp"),
        Blocked("player.attr", "V8PlayerImpl.cpp"),
        Blocked("player.colors", "V8PlayerImpl.cpp"),
        Blocked("player.flags", "V8PlayerImpl.cpp"),
        Blocked("npc", "V8NPCImpl.cpp"),
        Blocked("npc.attr", "V8NPCImpl.cpp"),
        Blocked("npc.colors", "V8NPCImpl.cpp"),
        Blocked("npc.flags", "V8NPCImpl.cpp"),
        Blocked("npc.save", "V8NPCImpl.cpp"),
        Blocked("level", "V8LevelImpl.cpp"),
        Blocked("level.tiles", "V8LevelImpl.cpp"),
        Blocked("level.links", "V8LevelImpl.cpp"),
        Blocked("level.signs", "V8LevelImpl.cpp"),
        Blocked("level.chests", "V8LevelImpl.cpp"),
        Blocked("level.npcs", "V8LevelImpl.cpp"),
        Blocked("level.link", "V8LevelLinkImpl.cpp"),
        Blocked("level.sign", "V8LevelSignImpl.cpp"),
        Blocked("level.chest", "V8LevelChestImpl.cpp"),
        Blocked("weapon", "V8WeaponImpl.cpp")
    ];

    private static ScriptVisibleApiStatus Blocked(string name, string sourceFile) =>
        new(name, sourceFile, IsImplemented: false, RuntimeBlocker);
}

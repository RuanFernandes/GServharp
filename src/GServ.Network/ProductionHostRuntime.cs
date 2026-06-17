using GServ.Game;

namespace GServ.Network;

public interface IProductionHostRuntime
{
    bool V8NpcServerEnabled { get; }

    void UpdateSockets();
    void RunScripts();
    void RunServerListTimedEvents();
    void RunPlayerTimedEvents();
    void RunLevelTimedEvents();
    void RunGroupLevelTimedEvents();
    uint CalculateServerTime(TimeSpan currentTimeUtc);
    void BroadcastNewWorldTime(uint serverTime);
    void SaveServerFlags();
    void ResyncFileSystemAccounts();
    void ResyncFileSystems();
    void LoadAllowedVersions();
    void LoadServerMessage();
    void LoadIpBans();
    void SaveWeapons();
    void SaveNpcs();
    void CleanupEmptyInstancedGroupLevels();
    void CleanupDeletedPlayers();
    bool Initialize();
    void Cleanup();
}

public sealed class ProductionHostRuntime : IProductionHostRuntime
{
    private readonly RuntimeServer? _runtimeServer;

    public bool V8NpcServerEnabled { get; }

    public Action SocketManagerUpdateHandler { get; set; } = () => { };
    public Action RunScriptsHandler { get; set; } = () => { };
    public Action ServerListTimedEventsHandler { get; set; } = () => { };
    public Action PlayerTimedEventsHandler { get; set; } = () => { };
    public Action LevelTimedEventsHandler { get; set; } = () => { };
    public Action GroupLevelTimedEventsHandler { get; set; } = () => { };
    public Action SaveServerFlagsHandler { get; set; } = () => { };
    public Action ResyncFileSystemAccountsHandler { get; set; } = () => { };
    public Action ResyncFileSystemsHandler { get; set; } = () => { };
    public Action LoadAllowedVersionsHandler { get; set; } = () => { };
    public Action LoadServerMessageHandler { get; set; } = () => { };
    public Action LoadIpBansHandler { get; set; } = () => { };
    public Action SaveWeaponsHandler { get; set; } = () => { };
    public Action SaveNpcsHandler { get; set; } = () => { };
    public Action CleanupEmptyInstancedGroupLevelsHandler { get; set; } = () => { };
    public Action BroadcastNewWorldTimeHandler { get; set; } = () => { };
    public Func<bool> InitializeHandler { get; set; } = () => true;
    public Action CleanupHandler { get; set; } = () => { };

    public Func<RuntimePlayer, bool>? ScriptObjectReferenceGate { get; set; }

    public Func<RuntimeServer, bool> IsScriptAwareCleanupAllowed { get; set; } = _ => true;
    public Action<RuntimePlayer>? ScriptObjectReferencedCallback { get; set; }
    public Action<RuntimePlayer>? BeforeRuntimePlayerDeleteCallback { get; set; }

    public ProductionHostRuntime(
        RuntimeServer? runtimeServer = null,
        bool v8NpcServerEnabled = false)
    {
        _runtimeServer = runtimeServer;
        V8NpcServerEnabled = v8NpcServerEnabled;
    }

    public void UpdateSockets() => SocketManagerUpdateHandler();

    public void RunScripts() => RunScriptsHandler();

    public void RunServerListTimedEvents() => ServerListTimedEventsHandler();

    public void RunPlayerTimedEvents() => PlayerTimedEventsHandler();

    public void RunLevelTimedEvents() => LevelTimedEventsHandler();

    public void RunGroupLevelTimedEvents() => GroupLevelTimedEventsHandler();

    public uint CalculateServerTime(TimeSpan currentTimeUtc) =>
        NewWorldTimeClock.Calculate((long)currentTimeUtc.TotalSeconds);

    public void BroadcastNewWorldTime(uint serverTime)
    {
        BroadcastNewWorldTimeHandler();
    }

    public void SaveServerFlags() => SaveServerFlagsHandler();

    public void ResyncFileSystemAccounts() => ResyncFileSystemAccountsHandler();

    public void ResyncFileSystems() => ResyncFileSystemsHandler();

    public void LoadAllowedVersions() => LoadAllowedVersionsHandler();

    public void LoadServerMessage() => LoadServerMessageHandler();

    public void LoadIpBans() => LoadIpBansHandler();

    public void SaveWeapons() => SaveWeaponsHandler();

    public void SaveNpcs() => SaveNpcsHandler();

    public void CleanupEmptyInstancedGroupLevels() => CleanupEmptyInstancedGroupLevelsHandler();

    public void CleanupDeletedPlayers()
    {
        if (_runtimeServer is null)
            return;

        var scriptObjectRefGate = IsScriptAwareCleanupAllowed(_runtimeServer)
            ? ScriptObjectReferenceGate
            : null;

        _runtimeServer.CleanupDeletedPlayers(
            scriptObjectRefGate,
            scriptObjectRefGate is null ? null : ScriptObjectReferencedCallback,
            BeforeRuntimePlayerDeleteCallback);
    }

    public bool Initialize() => InitializeHandler();

    public void Cleanup() => CleanupHandler();
}

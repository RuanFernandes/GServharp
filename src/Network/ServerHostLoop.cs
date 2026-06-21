namespace Preagonal.GServer.Network;

public sealed class ServerHostLoop
{
    private readonly IServerHostRuntime _runtime;
    private readonly ServerTimingScheduler _timingScheduler;
    private readonly Func<TimeSpan> _timeSource;
    private bool _requestedRestart;
    private bool _requestedShutdown;
    private bool _isRunning;

    public ServerHostLoop(
        IServerHostRuntime runtime,
        Func<TimeSpan>? timeSource = null,
        TimeSpan? start = null)
    {
        _runtime = runtime;
        _timeSource = timeSource ?? StaticTime;
        _timingScheduler = new ServerTimingScheduler(start ?? _timeSource(), runtime.Gs2NpcServerEnabled);
    }

    public bool IsRunning => _isRunning;

    public void RequestRestart() => _requestedRestart = true;

    public void RequestShutdown() => _requestedShutdown = true;

    public bool RunOneIteration()
    {
        var now = _timeSource();
        var actions = _timingScheduler.Tick(now);
        uint lastCalculatedWorldTime = 0;
        var hasWorldTime = false;

        foreach (var action in actions)
        {
            switch (action)
            {
                case ServerTimingAction.SocketManagerUpdate5ms:
                    _runtime.UpdateSockets();
                    break;
                case ServerTimingAction.RunScripts:
                    _runtime.RunScripts();
                    break;
                case ServerTimingAction.ServerListTimedEvents:
                    _runtime.RunServerListTimedEvents();
                    break;
                case ServerTimingAction.PlayerTimedEvents:
                    _runtime.RunPlayerTimedEvents();
                    break;
                case ServerTimingAction.LevelTimedEvents:
                    _runtime.RunLevelTimedEvents();
                    break;
                case ServerTimingAction.GroupLevelTimedEvents:
                    _runtime.RunGroupLevelTimedEvents();
                    break;
                case ServerTimingAction.CalculateServerTime:
                    lastCalculatedWorldTime = _runtime.CalculateServerTime(now);
                    hasWorldTime = true;
                    break;
                case ServerTimingAction.BroadcastNewWorldTime:
                    _runtime.BroadcastNewWorldTime(hasWorldTime
                        ? lastCalculatedWorldTime
                        : _runtime.CalculateServerTime(now));
                    break;
                case ServerTimingAction.SaveServerFlags:
                    _runtime.SaveServerFlags();
                    break;
                case ServerTimingAction.ResyncFileSystemAccounts:
                    _runtime.ResyncFileSystemAccounts();
                    break;
                case ServerTimingAction.ResyncFileSystems:
                    _runtime.ResyncFileSystems();
                    break;
                case ServerTimingAction.LoadAllowedVersions:
                    _runtime.LoadAllowedVersions();
                    break;
                case ServerTimingAction.LoadServerMessage:
                    _runtime.LoadServerMessage();
                    break;
                case ServerTimingAction.LoadIpBans:
                    _runtime.LoadIpBans();
                    break;
                case ServerTimingAction.SaveWeapons:
                    _runtime.SaveWeapons();
                    break;
                case ServerTimingAction.SaveNpcs:
                    _runtime.SaveNpcs();
                    break;
                case ServerTimingAction.CleanupEmptyInstancedGroupLevels:
                    _runtime.CleanupEmptyInstancedGroupLevels();
                    break;
            }
        }

        _runtime.CleanupDeletedPlayers();

        if (_requestedRestart)
        {
            _requestedRestart = false;
            _runtime.Cleanup();
            if (!_runtime.Initialize())
                return false;
        }

        return !_requestedShutdown;
    }

    public void Run(TimeSpan iterationDelay, CancellationToken cancellationToken = default)
    {
        _isRunning = true;
        if (!_runtime.Initialize())
        {
            _runtime.Cleanup();
            _isRunning = false;
            return;
        }

        try
        {
            while (_isRunning && !cancellationToken.IsCancellationRequested)
            {
                if (!RunOneIteration())
                    break;

                if (iterationDelay > TimeSpan.Zero)
                    Thread.Sleep(iterationDelay);
            }
        }
        finally
        {
            _runtime.Cleanup();
            _isRunning = false;
        }
    }

    public static TimeSpan StaticTime()
    {
        return DateTimeOffset.UtcNow - DateTimeOffset.UnixEpoch;
    }
}

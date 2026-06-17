namespace GServ.Network;

public sealed class ProductionHostLoop
{
    private readonly IProductionHostRuntime _runtime;
    private readonly ProductionTimingScheduler _timingScheduler;
    private readonly Func<TimeSpan> _timeSource;
    private bool _requestedRestart;
    private bool _requestedShutdown;
    private bool _isRunning;

    public ProductionHostLoop(
        IProductionHostRuntime runtime,
        Func<TimeSpan>? timeSource = null,
        TimeSpan? start = null)
    {
        _runtime = runtime;
        _timeSource = timeSource ?? StaticTime;
        _timingScheduler = new ProductionTimingScheduler(start ?? _timeSource(), runtime.V8NpcServerEnabled);
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
                case ProductionTimingAction.SocketManagerUpdate5ms:
                    _runtime.UpdateSockets();
                    break;
                case ProductionTimingAction.RunScripts:
                    _runtime.RunScripts();
                    break;
                case ProductionTimingAction.ServerListTimedEvents:
                    _runtime.RunServerListTimedEvents();
                    break;
                case ProductionTimingAction.PlayerTimedEvents:
                    _runtime.RunPlayerTimedEvents();
                    break;
                case ProductionTimingAction.LevelTimedEvents:
                    _runtime.RunLevelTimedEvents();
                    break;
                case ProductionTimingAction.GroupLevelTimedEvents:
                    _runtime.RunGroupLevelTimedEvents();
                    break;
                case ProductionTimingAction.CalculateServerTime:
                    lastCalculatedWorldTime = _runtime.CalculateServerTime(now);
                    hasWorldTime = true;
                    break;
                case ProductionTimingAction.BroadcastNewWorldTime:
                    _runtime.BroadcastNewWorldTime(hasWorldTime
                        ? lastCalculatedWorldTime
                        : _runtime.CalculateServerTime(now));
                    break;
                case ProductionTimingAction.SaveServerFlags:
                    _runtime.SaveServerFlags();
                    break;
                case ProductionTimingAction.ResyncFileSystemAccounts:
                    _runtime.ResyncFileSystemAccounts();
                    break;
                case ProductionTimingAction.ResyncFileSystems:
                    _runtime.ResyncFileSystems();
                    break;
                case ProductionTimingAction.LoadAllowedVersions:
                    _runtime.LoadAllowedVersions();
                    break;
                case ProductionTimingAction.LoadServerMessage:
                    _runtime.LoadServerMessage();
                    break;
                case ProductionTimingAction.LoadIpBans:
                    _runtime.LoadIpBans();
                    break;
                case ProductionTimingAction.SaveWeapons:
                    _runtime.SaveWeapons();
                    break;
                case ProductionTimingAction.SaveNpcs:
                    _runtime.SaveNpcs();
                    break;
                case ProductionTimingAction.CleanupEmptyInstancedGroupLevels:
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

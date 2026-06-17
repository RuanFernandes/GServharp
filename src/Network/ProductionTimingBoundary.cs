using GServ.Protocol;

namespace GServ.Network;

public enum ProductionTimingAction
{
    SocketManagerUpdate5ms,
    RunScripts,
    ServerListTimedEvents,
    PlayerTimedEvents,
    LevelTimedEvents,
    GroupLevelTimedEvents,
    CalculateServerTime,
    BroadcastNewWorldTime,
    SaveServerFlags,
    ResyncFileSystemAccounts,
    ResyncFileSystems,
    LoadAllowedVersions,
    LoadServerMessage,
    LoadIpBans,
    SaveWeapons,
    SaveNpcs,
    CleanupEmptyInstancedGroupLevels
}

public sealed class ProductionTimingScheduler
{
    private TimeSpan _lastTimer;
    private TimeSpan _lastNewWorldTimer;
    private TimeSpan _lastOneMinuteTimer;
    private TimeSpan _lastThreeMinuteTimer;
    private TimeSpan _lastFiveMinuteTimer;

    public ProductionTimingScheduler(TimeSpan start, bool v8NpcServerEnabled = false)
    {
        _lastTimer = start;
        _lastNewWorldTimer = start;
        _lastOneMinuteTimer = start;
        _lastThreeMinuteTimer = start;
        _lastFiveMinuteTimer = start;
        V8NpcServerEnabled = v8NpcServerEnabled;
    }

    public bool V8NpcServerEnabled { get; }

    public IReadOnlyList<ProductionTimingAction> Tick(TimeSpan currentTimer)
    {
        var actions = new List<ProductionTimingAction>
        {
            ProductionTimingAction.SocketManagerUpdate5ms
        };

        if (V8NpcServerEnabled)
            actions.Add(ProductionTimingAction.RunScripts);

        if (currentTimer - _lastTimer < TimeSpan.FromSeconds(1))
            return actions;

        _lastTimer = currentTimer;
        AddTimedEvents(actions);
        return actions;
    }

    private void AddTimedEvents(List<ProductionTimingAction> actions)
    {
        actions.Add(ProductionTimingAction.ServerListTimedEvents);
        actions.Add(ProductionTimingAction.PlayerTimedEvents);
        actions.Add(ProductionTimingAction.LevelTimedEvents);
        actions.Add(ProductionTimingAction.GroupLevelTimedEvents);

        if (_lastTimer - _lastNewWorldTimer >= TimeSpan.FromSeconds(5))
        {
            actions.Add(ProductionTimingAction.CalculateServerTime);
            _lastNewWorldTimer = _lastTimer;
            actions.Add(ProductionTimingAction.BroadcastNewWorldTime);
        }

        if (_lastTimer - _lastOneMinuteTimer >= TimeSpan.FromSeconds(60))
        {
            _lastOneMinuteTimer = _lastTimer;
            actions.Add(ProductionTimingAction.SaveServerFlags);
        }

        if (_lastTimer - _lastThreeMinuteTimer >= TimeSpan.FromSeconds(180))
        {
            _lastThreeMinuteTimer = _lastTimer;
            actions.Add(ProductionTimingAction.ResyncFileSystemAccounts);
            actions.Add(ProductionTimingAction.ResyncFileSystems);
        }

        if (_lastTimer - _lastFiveMinuteTimer >= TimeSpan.FromSeconds(300))
        {
            _lastFiveMinuteTimer = _lastTimer;
            actions.Add(ProductionTimingAction.LoadAllowedVersions);
            actions.Add(ProductionTimingAction.LoadServerMessage);
            actions.Add(ProductionTimingAction.LoadIpBans);
            actions.Add(ProductionTimingAction.SaveWeapons);

            if (V8NpcServerEnabled)
                actions.Add(ProductionTimingAction.SaveNpcs);

            actions.Add(ProductionTimingAction.CleanupEmptyInstancedGroupLevels);
        }
    }
}

public enum PlayerTimedEventAction
{
    DeleteDisconnectedSocket,
    IncrementOnlineTime,
    DisconnectForInactivity,
    DisconnectNoDataTimeout,
    RunApRecovery,
    RunSingleplayerLevelTimedEvents,
    SaveAccount,
    ResetInvalidPackets,
    SendFileQueueCompress
}

public sealed record PlayerTimedEventState(
    TimeSpan LastMovement,
    TimeSpan LastChat,
    TimeSpan LastData,
    TimeSpan LastSave,
    TimeSpan LastInvalidPacketReset)
{
    public const string InactivityDisconnectMessage = "You have been disconnected due to inactivity.";

    public bool SocketConnected { get; set; } = true;
    public bool IsClient { get; set; } = true;
    public bool DisconnectIfNotMoved { get; set; }
    public int MaxNoMovementSeconds { get; set; } = 1200;
    public bool IsLoaded { get; set; }
    public bool IsLoadOnly { get; set; }
    public int InvalidPackets { get; set; }
    public int OnlineTime { get; private set; }
    public TimeSpan LastSave { get; private set; } = LastSave;
    public TimeSpan LastInvalidPacketReset { get; private set; } = LastInvalidPacketReset;

    public IReadOnlyList<PlayerTimedEventAction> Tick(TimeSpan currentTime)
    {
        if (!SocketConnected)
            return new[] { PlayerTimedEventAction.DeleteDisconnectedSocket };

        if (!IsClient)
            return Array.Empty<PlayerTimedEventAction>();

        var actions = new List<PlayerTimedEventAction>
        {
            PlayerTimedEventAction.IncrementOnlineTime
        };
        OnlineTime++;

        if (DisconnectIfNotMoved &&
            currentTime - LastMovement > TimeSpan.FromSeconds(MaxNoMovementSeconds) &&
            currentTime - LastChat > TimeSpan.FromSeconds(MaxNoMovementSeconds))
        {
            actions.Add(PlayerTimedEventAction.DisconnectForInactivity);
            return actions;
        }

        if (currentTime - LastData > TimeSpan.FromSeconds(300))
        {
            actions.Add(PlayerTimedEventAction.DisconnectNoDataTimeout);
            return actions;
        }

        actions.Add(PlayerTimedEventAction.RunApRecovery);
        actions.Add(PlayerTimedEventAction.RunSingleplayerLevelTimedEvents);

        if (currentTime - LastSave > TimeSpan.FromSeconds(300))
        {
            LastSave = currentTime;
            if (IsLoaded && !IsLoadOnly)
                actions.Add(PlayerTimedEventAction.SaveAccount);
        }

        if (currentTime - LastInvalidPacketReset > TimeSpan.FromSeconds(60))
        {
            LastInvalidPacketReset = currentTime;
            InvalidPackets = 0;
            actions.Add(PlayerTimedEventAction.ResetInvalidPackets);
        }

        actions.Add(PlayerTimedEventAction.SendFileQueueCompress);
        return actions;
    }
}

public interface IServerListReconnectJitterSource
{
    int NextJitterSeconds();
}

public sealed class ServerListReconnectState
{
    public int ConnectionAttempts { get; private set; }
    public TimeSpan NextConnectionAttempt { get; private set; }

    public TimeSpan ConnectionFailed(TimeSpan lastTimer, IServerListReconnectJitterSource jitterSource)
    {
        if (ConnectionAttempts < 8)
            ConnectionAttempts += 1;

        var waitTimeSeconds = Math.Min(Math.Pow(2, ConnectionAttempts), 300);
        var jitterSeconds = jitterSource.NextJitterSeconds();
        NextConnectionAttempt = lastTimer + TimeSpan.FromSeconds(waitTimeSeconds + jitterSeconds);
        return NextConnectionAttempt;
    }

    public void ConnectionSucceeded()
    {
        ConnectionAttempts = 0;
    }
}

public static class NewWorldTimeClock
{
    private const uint NewWorldTimeEpoch = 981048814;

    public static uint Calculate(long unixSeconds)
    {
        return unchecked((uint)(unixSeconds - NewWorldTimeEpoch)) / 5;
    }
}

public static class ProductionTimingPackets
{
    public static byte[] BuildNewWorldTime(uint newWorldTime)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.NewWorldTime);
        writer.WriteGInt4(newWorldTime);
        return writer.ToArray();
    }
}

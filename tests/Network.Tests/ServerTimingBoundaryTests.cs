using Preagonal.GServer.Network;
using Preagonal.GServer.Protocol;
using Xunit;

namespace Preagonal.GServer.Network.Tests;

public sealed class ServerTimingBoundaryTests
{
    [Fact]
    public void DoMainAlwaysSchedulesSocketUpdateBeforeOneSecondTimedEvents()
    {
        var scheduler = new ServerTimingScheduler(TimeSpan.Zero);

        var actions = scheduler.Tick(TimeSpan.FromSeconds(1));

        Assert.Equal(
            new[]
            {
                ServerTimingAction.SocketManagerUpdate5ms,
                ServerTimingAction.ServerListTimedEvents,
                ServerTimingAction.PlayerTimedEvents,
                ServerTimingAction.LevelTimedEvents,
                ServerTimingAction.GroupLevelTimedEvents
            },
            actions);
    }

    [Fact]
    public void DoMainDoesNotRunOneSecondTimedEventsBeforeOneSecondElapsed()
    {
        var scheduler = new ServerTimingScheduler(TimeSpan.Zero);

        var actions = scheduler.Tick(TimeSpan.FromMilliseconds(999));

        Assert.Equal(new[] { ServerTimingAction.SocketManagerUpdate5ms }, actions);
    }

    [Fact]
    public void PeriodicServerJobsPreserveCppOrderAtFiveMinutes()
    {
        var scheduler = new ServerTimingScheduler(TimeSpan.Zero);

        var actions = scheduler.Tick(TimeSpan.FromMinutes(5));

        Assert.Equal(
            new[]
            {
                ServerTimingAction.SocketManagerUpdate5ms,
                ServerTimingAction.ServerListTimedEvents,
                ServerTimingAction.PlayerTimedEvents,
                ServerTimingAction.LevelTimedEvents,
                ServerTimingAction.GroupLevelTimedEvents,
                ServerTimingAction.CalculateServerTime,
                ServerTimingAction.BroadcastNewWorldTime,
                ServerTimingAction.SaveServerFlags,
                ServerTimingAction.ResyncFileSystemAccounts,
                ServerTimingAction.ResyncFileSystems,
                ServerTimingAction.LoadAllowedVersions,
                ServerTimingAction.LoadServerMessage,
                ServerTimingAction.LoadIpBans,
                ServerTimingAction.SaveWeapons,
                ServerTimingAction.CleanupEmptyInstancedGroupLevels
            },
            actions);
    }

    [Fact]
    public void OneSecondTimerResetsToCurrentTickLikeCpp()
    {
        var scheduler = new ServerTimingScheduler(TimeSpan.Zero);

        _ = scheduler.Tick(TimeSpan.FromMilliseconds(1500));
        var actions = scheduler.Tick(TimeSpan.FromMilliseconds(2400));

        Assert.Equal(new[] { ServerTimingAction.SocketManagerUpdate5ms }, actions);
    }

    [Fact]
    public void ServerListReconnectBackoffMatchesCppCapAndJitterWindow()
    {
        var state = new ServerListReconnectState();
        var random = new SequenceJitterSource(4, 0);

        var first = state.ConnectionFailed(TimeSpan.FromSeconds(10), random);
        Assert.Equal(1, state.ConnectionAttempts);
        Assert.Equal(TimeSpan.FromSeconds(10 + 2 + 4), first);

        var second = state.ConnectionFailed(TimeSpan.FromSeconds(20), random);
        Assert.Equal(2, state.ConnectionAttempts);
        Assert.Equal(TimeSpan.FromSeconds(20 + 4 + 0), second);

        for (var i = 0; i < 16; i++)
            state.ConnectionFailed(TimeSpan.FromSeconds(30 + i), random);

        Assert.Equal(8, state.ConnectionAttempts);
        Assert.True(state.NextConnectionAttempt <= TimeSpan.FromSeconds(45 + 300 + 4));
    }

    [Fact]
    public void ConnectedServerListClearsReconnectAttempts()
    {
        var state = new ServerListReconnectState();
        state.ConnectionFailed(TimeSpan.FromSeconds(1), new SequenceJitterSource(0));

        state.ConnectionSucceeded();

        Assert.Equal(0, state.ConnectionAttempts);
    }

    [Fact]
    public void PlayerTimedEventsDisconnectDisconnectedSocketBeforeClientChecks()
    {
        var state = new PlayerTimedEventState(
            LastMovement: TimeSpan.Zero,
            LastChat: TimeSpan.Zero,
            LastData: TimeSpan.Zero,
            LastSave: TimeSpan.Zero,
            LastInvalidPacketReset: TimeSpan.Zero)
        {
            SocketConnected = false,
            IsClient = true
        };

        var actions = state.Tick(TimeSpan.FromSeconds(1));

        Assert.Equal(new[] { PlayerTimedEventAction.DeleteDisconnectedSocket }, actions);
    }

    [Fact]
    public void PlayerTimedEventsUseStrictGreaterThanThresholds()
    {
        var state = new PlayerTimedEventState(
            LastMovement: TimeSpan.Zero,
            LastChat: TimeSpan.Zero,
            LastData: TimeSpan.Zero,
            LastSave: TimeSpan.Zero,
            LastInvalidPacketReset: TimeSpan.Zero)
        {
            DisconnectIfNotMoved = true,
            IsLoaded = true,
            IsLoadOnly = false
        };

        var atThreshold = state.Tick(TimeSpan.FromSeconds(300));
        Assert.DoesNotContain(PlayerTimedEventAction.DisconnectNoDataTimeout, atThreshold);
        Assert.DoesNotContain(PlayerTimedEventAction.SaveAccount, atThreshold);

        var afterThreshold = state.Tick(TimeSpan.FromSeconds(301));
        Assert.Contains(PlayerTimedEventAction.DisconnectNoDataTimeout, afterThreshold);
    }

    [Fact]
    public void PlayerTimedEventsSaveAndResetInvalidPacketsAtConfirmedIntervals()
    {
        var state = new PlayerTimedEventState(
            LastMovement: TimeSpan.FromSeconds(500),
            LastChat: TimeSpan.FromSeconds(500),
            LastData: TimeSpan.FromSeconds(500),
            LastSave: TimeSpan.Zero,
            LastInvalidPacketReset: TimeSpan.Zero)
        {
            IsLoaded = true,
            IsLoadOnly = false,
            InvalidPackets = 5
        };

        var actions = state.Tick(TimeSpan.FromSeconds(301));

        Assert.Contains(PlayerTimedEventAction.SaveAccount, actions);
        Assert.Contains(PlayerTimedEventAction.ResetInvalidPackets, actions);
        Assert.Contains(PlayerTimedEventAction.SendFileQueueCompress, actions);
        Assert.Equal(TimeSpan.FromSeconds(301), state.LastSave);
        Assert.Equal(TimeSpan.FromSeconds(301), state.LastInvalidPacketReset);
        Assert.Equal(0, state.InvalidPackets);
    }

    [Fact]
    public void NewWorldTimeCalculationAndPacketMatchConfirmedCppEncoding()
    {
        var time = NewWorldTimeClock.Calculate(unixSeconds: 981048819);
        var packet = ServerTimingPackets.BuildNewWorldTime(time);

        Assert.Equal((uint)1, time);
        Assert.Equal(new byte[] { 74, 32, 32, 32, 33 }, packet);
    }

    private sealed class SequenceJitterSource : IServerListReconnectJitterSource
    {
        private readonly Queue<int> _values;

        public SequenceJitterSource(params int[] values)
        {
            _values = new Queue<int>(values);
        }

        public int NextJitterSeconds()
        {
            return _values.Count == 0 ? 0 : _values.Dequeue();
        }
    }
}

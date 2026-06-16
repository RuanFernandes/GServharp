using System.Collections.Generic;
using GServ.Network;
using Xunit;

namespace GServ.Network.Tests;

public sealed class ProductionHostLoopTests
{
    [Fact]
    public void RunOneIterationExecutesSourceConfirmedActionOrderAndCleanup()
    {
        var clock = new FakeHostClock();
        var runtime = new RecordingProductionHostRuntime();
        var loop = new ProductionHostLoop(runtime, clock.Next);

        clock.Current = TimeSpan.Zero;
        loop.RunOneIteration();

        clock.Current = TimeSpan.FromSeconds(1);
        loop.RunOneIteration();

        var expected = new[]
        {
            ProductionTimingAction.SocketManagerUpdate5ms,
            ProductionTimingAction.SocketManagerUpdate5ms,
            ProductionTimingAction.ServerListTimedEvents,
            ProductionTimingAction.PlayerTimedEvents,
            ProductionTimingAction.LevelTimedEvents,
            ProductionTimingAction.GroupLevelTimedEvents,
        };

        Assert.Equal(expected.Length, runtime.ActionsLog.Count);
        for (var i = 0; i < expected.Length; i++)
            Assert.Equal(expected[i], runtime.ActionsLog[i]);
        Assert.Equal(2, runtime.CleanupDeletedPlayersCount);
    }

    [Fact]
    public void RestartRequestsRunCleanupThenReinitializeBeforeContinuing()
    {
        var clock = new FakeHostClock();
        var runtime = new RecordingProductionHostRuntime { InitializeResult = true };
        var loop = new ProductionHostLoop(runtime, clock.Next);

        clock.Current = TimeSpan.FromSeconds(1);
        loop.RequestRestart();
        var shouldContinue = loop.RunOneIteration();

        Assert.True(shouldContinue);
        Assert.True(runtime.CleanupCalled);
        Assert.True(runtime.InitializeCalled);
        Assert.Equal(1, runtime.CleanupCount);
        Assert.Equal(1, runtime.InitializeCount);
    }

    [Fact]
    public void RestartFailureStopsTheLoopAtNextIterationBoundary()
    {
        var clock = new FakeHostClock();
        var runtime = new RecordingProductionHostRuntime { InitializeResult = false };
        var loop = new ProductionHostLoop(runtime, clock.Next);

        clock.Current = TimeSpan.FromSeconds(1);
        loop.RequestRestart();
        var shouldContinue = loop.RunOneIteration();

        Assert.False(shouldContinue);
        Assert.True(runtime.CleanupCalled);
        Assert.True(runtime.InitializeCalled);
    }

    [Fact]
    public void ShutdownRequestStopsWithoutFurtherIterations()
    {
        var clock = new FakeHostClock();
        var runtime = new RecordingProductionHostRuntime();
        using var cts = new CancellationTokenSource();

        var loop = new ProductionHostLoop(runtime, clock.Next);

        cts.CancelAfter(1);

        loop.RequestShutdown();
        loop.Run(TimeSpan.Zero, cts.Token);

        Assert.False(loop.IsRunning);
        Assert.True(runtime.CleanupCalled);
    }

    private sealed class RecordingProductionHostRuntime : IProductionHostRuntime
    {
        public bool V8NpcServerEnabled { get; }

        public readonly List<ProductionTimingAction> Actions = [];
        public bool InitializeResult { get; init; } = true;
        public bool CleanupCalled { get; private set; }
        public bool InitializeCalled { get; private set; }
        public int CleanupCount { get; private set; }
        public int InitializeCount { get; private set; }
        public int CleanupDeletedPlayersCount { get; private set; }
        public IReadOnlyList<ProductionTimingAction> ActionsLog => Actions;

        public void UpdateSockets() => Actions.Add(ProductionTimingAction.SocketManagerUpdate5ms);
        public void RunScripts() => Actions.Add(ProductionTimingAction.RunScripts);
        public void RunServerListTimedEvents() => Actions.Add(ProductionTimingAction.ServerListTimedEvents);
        public void RunPlayerTimedEvents() => Actions.Add(ProductionTimingAction.PlayerTimedEvents);
        public void RunLevelTimedEvents() => Actions.Add(ProductionTimingAction.LevelTimedEvents);
        public void RunGroupLevelTimedEvents() => Actions.Add(ProductionTimingAction.GroupLevelTimedEvents);
        public uint CalculateServerTime(TimeSpan currentTimeUtc) { Actions.Add(ProductionTimingAction.CalculateServerTime); return 1; }

        public void BroadcastNewWorldTime(uint serverTime)
        {
            Actions.Add(ProductionTimingAction.BroadcastNewWorldTime);
        }

        public void SaveServerFlags() => Actions.Add(ProductionTimingAction.SaveServerFlags);
        public void ResyncFileSystemAccounts() => Actions.Add(ProductionTimingAction.ResyncFileSystemAccounts);
        public void ResyncFileSystems() => Actions.Add(ProductionTimingAction.ResyncFileSystems);
        public void LoadAllowedVersions() => Actions.Add(ProductionTimingAction.LoadAllowedVersions);
        public void LoadServerMessage() => Actions.Add(ProductionTimingAction.LoadServerMessage);
        public void LoadIpBans() => Actions.Add(ProductionTimingAction.LoadIpBans);
        public void SaveWeapons() => Actions.Add(ProductionTimingAction.SaveWeapons);
        public void SaveNpcs() => Actions.Add(ProductionTimingAction.SaveNpcs);
        public void CleanupEmptyInstancedGroupLevels() => Actions.Add(ProductionTimingAction.CleanupEmptyInstancedGroupLevels);
        public void CleanupDeletedPlayers() => CleanupDeletedPlayersCount++;

        public bool Initialize()
        {
            InitializeCalled = true;
            InitializeCount++;
            return InitializeResult;
        }

        public void Cleanup()
        {
            CleanupCalled = true;
            CleanupCount++;
        }
    }

    private sealed class FakeHostClock
    {
        public TimeSpan Current { get; set; }
        public TimeSpan Next() => Current;
    }

}

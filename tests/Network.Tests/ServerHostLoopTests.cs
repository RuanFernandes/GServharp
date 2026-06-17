using System.Collections.Generic;
using GServ.Network;
using GServ.Game;
using Xunit;

namespace GServ.Network.Tests;

    public sealed class ServerHostLoopTests
    {
        [Fact]
        public void RunOneIterationExecutesSourceConfirmedActionOrderAndCleanup()
    {
        var clock = new FakeHostClock();
        var runtime = new RecordingServerHostRuntime();
        var loop = new ServerHostLoop(runtime, clock.Next);

        clock.Current = TimeSpan.Zero;
        loop.RunOneIteration();

        clock.Current = TimeSpan.FromSeconds(1);
        loop.RunOneIteration();

        var expected = new[]
        {
            ServerTimingAction.SocketManagerUpdate5ms,
            ServerTimingAction.SocketManagerUpdate5ms,
            ServerTimingAction.ServerListTimedEvents,
            ServerTimingAction.PlayerTimedEvents,
            ServerTimingAction.LevelTimedEvents,
            ServerTimingAction.GroupLevelTimedEvents,
        };

        Assert.Equal(expected.Length, runtime.ActionsLog.Count);
        for (var i = 0; i < expected.Length; i++)
            Assert.Equal(expected[i], runtime.ActionsLog[i]);
        Assert.Equal(2, runtime.CleanupDeletedPlayersCount);
    }

    [Fact]
    public void RunCallsInitializeBeforeLoopAndCleanupWhenInitializeFails()
    {
        var clock = new FakeHostClock();
        var runtime = new RecordingServerHostRuntime { InitializeResult = false };
        var loop = new ServerHostLoop(runtime, clock.Next);

        loop.Run(TimeSpan.Zero);

        Assert.True(runtime.InitializeCalled);
        Assert.Equal(1, runtime.InitializeCount);
        Assert.True(runtime.CleanupCalled);
        Assert.Equal(1, runtime.CleanupCount);
        Assert.False(loop.IsRunning);
    }

    [Fact]
    public void RestartRequestsRunCleanupThenReinitializeBeforeContinuing()
    {
        var clock = new FakeHostClock();
        var runtime = new RecordingServerHostRuntime { InitializeResult = true };
        var loop = new ServerHostLoop(runtime, clock.Next);

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
        var runtime = new RecordingServerHostRuntime { InitializeResult = false };
        var loop = new ServerHostLoop(runtime, clock.Next);

        clock.Current = TimeSpan.FromSeconds(1);
        loop.RequestRestart();
        var shouldContinue = loop.RunOneIteration();

        Assert.False(shouldContinue);
        Assert.True(runtime.CleanupCalled);
        Assert.True(runtime.InitializeCalled);
    }

    [Fact]
    public void HostRuntimeCleanupDeletedPlayersPassesScriptReferenceHooks()
    {
        var server = new RuntimeServer();
        var player = new RuntimePlayer(2, "pc:player", RuntimePlayerKind.Client);
        server.AddPlayer(player, 2);
        server.DeletePlayer(player);

        var hostRuntime = new ServerHostRuntime(server);
        var referenced = false;
        var beforeDelete = false;

        hostRuntime.ScriptObjectReferenceGate = p => p.Id == 2;
        hostRuntime.ScriptObjectReferencedCallback = _ => referenced = true;
        hostRuntime.BeforeRuntimePlayerDeleteCallback = _ => beforeDelete = true;

        hostRuntime.CleanupDeletedPlayers();

        Assert.True(referenced);
        Assert.False(beforeDelete);
        Assert.NotNull(server.GetPlayer(2));

        referenced = false;
        beforeDelete = false;
        hostRuntime.ScriptObjectReferenceGate = p => false;
        hostRuntime.IsScriptAwareCleanupAllowed = _ => true;
        hostRuntime.CleanupDeletedPlayers();

        Assert.True(beforeDelete);
        Assert.False(referenced);
        Assert.Null(server.GetPlayer(2));
    }

    [Fact]
    public void ShutdownRequestStopsWithoutFurtherIterations()
    {
        var clock = new FakeHostClock();
        var runtime = new RecordingServerHostRuntime();
        using var cts = new CancellationTokenSource();

        var loop = new ServerHostLoop(runtime, clock.Next);

        cts.CancelAfter(1);

        loop.RequestShutdown();
        loop.Run(TimeSpan.Zero, cts.Token);

        Assert.False(loop.IsRunning);
        Assert.True(runtime.CleanupCalled);
    }

    private sealed class RecordingServerHostRuntime : IServerHostRuntime
    {
        public bool V8NpcServerEnabled { get; }

        public readonly List<ServerTimingAction> Actions = [];
        public bool InitializeResult { get; init; } = true;
        public bool CleanupCalled { get; private set; }
        public bool InitializeCalled { get; private set; }
        public int CleanupCount { get; private set; }
        public int InitializeCount { get; private set; }
        public int CleanupDeletedPlayersCount { get; private set; }
        public IReadOnlyList<ServerTimingAction> ActionsLog => Actions;

        public void UpdateSockets() => Actions.Add(ServerTimingAction.SocketManagerUpdate5ms);
        public void RunScripts() => Actions.Add(ServerTimingAction.RunScripts);
        public void RunServerListTimedEvents() => Actions.Add(ServerTimingAction.ServerListTimedEvents);
        public void RunPlayerTimedEvents() => Actions.Add(ServerTimingAction.PlayerTimedEvents);
        public void RunLevelTimedEvents() => Actions.Add(ServerTimingAction.LevelTimedEvents);
        public void RunGroupLevelTimedEvents() => Actions.Add(ServerTimingAction.GroupLevelTimedEvents);
        public uint CalculateServerTime(TimeSpan currentTimeUtc) { Actions.Add(ServerTimingAction.CalculateServerTime); return 1; }

        public void BroadcastNewWorldTime(uint serverTime)
        {
            Actions.Add(ServerTimingAction.BroadcastNewWorldTime);
        }

        public void SaveServerFlags() => Actions.Add(ServerTimingAction.SaveServerFlags);
        public void ResyncFileSystemAccounts() => Actions.Add(ServerTimingAction.ResyncFileSystemAccounts);
        public void ResyncFileSystems() => Actions.Add(ServerTimingAction.ResyncFileSystems);
        public void LoadAllowedVersions() => Actions.Add(ServerTimingAction.LoadAllowedVersions);
        public void LoadServerMessage() => Actions.Add(ServerTimingAction.LoadServerMessage);
        public void LoadIpBans() => Actions.Add(ServerTimingAction.LoadIpBans);
        public void SaveWeapons() => Actions.Add(ServerTimingAction.SaveWeapons);
        public void SaveNpcs() => Actions.Add(ServerTimingAction.SaveNpcs);
        public void CleanupEmptyInstancedGroupLevels() => Actions.Add(ServerTimingAction.CleanupEmptyInstancedGroupLevels);
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

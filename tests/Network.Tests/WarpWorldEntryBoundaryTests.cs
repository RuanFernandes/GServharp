using Preagonal.GServer.Network;
using Preagonal.GServer.Protocol;
using Xunit;

namespace Preagonal.GServer.Network.Tests;

public sealed class WarpWorldEntryBoundaryTests
{
    [Fact]
    public void BeginWarpQueuesSameLevelPositionPropsAndDoesNotEnterSendLevelRuntime()
    {
        var session = ReadyForLevelWarpSession();
        var levels = new MemoryLevelLookup();
        levels.Add(new LevelEntrySnapshot("start.nw"));

        var result = WarpWorldEntryBoundary.BeginWarp(
            session,
            levels,
            new PlayerWarpState(new LevelEntrySnapshot("start.nw"), CurrentX: 30.0f, CurrentY: 30.5f),
            new LevelWarpRequest("start.nw", X: 30.5f, Y: 31.25f, Z: 0, ClientVersionId.Client21, ModTime: 0),
            PlayerWarpSettings.Default);

        Assert.True(result.CppReturnValue);
        Assert.False(result.ReachedSendLevelRuntime);
        Assert.Equal(PlayerWarpStopPoint.SameLevelPositionUpdated, result.StopPoint);
        Assert.Equal(SessionLifecycle.SameLevelWarpPositionUpdated, session.Lifecycle);
        Assert.Equal(new byte[] { 41, 47, 93, 48, 94, 10 }, session.TakeOutboundBytes());
    }

    [Fact]
    public void BeginWarpFallsBackToPreviousLevelAfterMissingTargetButPreservesCppReturnFalse()
    {
        var session = ReadyForLevelWarpSession();
        var levels = new MemoryLevelLookup();
        levels.Add(new LevelEntrySnapshot("start.nw"));
        levels.Add(new LevelEntrySnapshot("onlinestartlocal.nw"));

        var result = WarpWorldEntryBoundary.BeginWarp(
            session,
            levels,
            new PlayerWarpState(new LevelEntrySnapshot("start.nw"), CurrentX: 30.5f, CurrentY: 31.25f),
            new LevelWarpRequest("missing.nw", X: 40.0f, Y: 41.0f, Z: 0, ClientVersionId.Client21, ModTime: 0),
            PlayerWarpSettings.Default);

        Assert.False(result.CppReturnValue);
        Assert.True(result.ReachedSendLevelRuntime);
        Assert.Equal(PlayerWarpStopPoint.FallbackPreviousReadyForSendLevelRuntime, result.StopPoint);
        Assert.Equal(SessionLifecycle.ReadyForLevelRuntime, session.Lifecycle);
        Assert.Equal(
            new byte[]
            {
                47, (byte)'m', (byte)'i', (byte)'s', (byte)'s', (byte)'i', (byte)'n', (byte)'g', (byte)'.', (byte)'n', (byte)'w', 10,
                46, 93, 94, (byte)'s', (byte)'t', (byte)'a', (byte)'r', (byte)'t', (byte)'.', (byte)'n', (byte)'w', 10
            },
            session.TakeOutboundBytes());
    }

    [Fact]
    public void BeginWarpFallsBackToUnstickLevelWhenPreviousLevelCannotBeRestored()
    {
        var session = ReadyForLevelWarpSession();
        var levels = new MemoryLevelLookup();
        levels.Add(new LevelEntrySnapshot("onlinestartlocal.nw"));

        var result = WarpWorldEntryBoundary.BeginWarp(
            session,
            levels,
            new PlayerWarpState(null, CurrentX: 30.5f, CurrentY: 31.25f),
            new LevelWarpRequest("missing.nw", X: 40.0f, Y: 41.0f, Z: 0, ClientVersionId.Client21, ModTime: 0),
            PlayerWarpSettings.Default);

        Assert.False(result.CppReturnValue);
        Assert.True(result.ReachedSendLevelRuntime);
        Assert.Equal(PlayerWarpStopPoint.FallbackUnstickReadyForSendLevelRuntime, result.StopPoint);
        Assert.Equal(
            new byte[]
            {
                47, (byte)'m', (byte)'i', (byte)'s', (byte)'s', (byte)'i', (byte)'n', (byte)'g', (byte)'.', (byte)'n', (byte)'w', 10,
                46, 92, 102, (byte)'o', (byte)'n', (byte)'l', (byte)'i', (byte)'n', (byte)'e', (byte)'s', (byte)'t', (byte)'a', (byte)'r', (byte)'t', (byte)'l', (byte)'o', (byte)'c', (byte)'a', (byte)'l', (byte)'.', (byte)'n', (byte)'w', 10
            },
            session.TakeOutboundBytes());
    }

    [Fact]
    public void BeginSetLevelQueuesWarpFailedWhenLevelLookupFails()
    {
        var session = ReadyForLevelWarpSession();
        var levels = new MemoryLevelLookup();

        var result = WarpWorldEntryBoundary.BeginSetLevel(
            session,
            levels,
            new LevelWarpRequest("missing.nw", X: 30.5f, Y: 31.25f, Z: 0, ClientVersionId.Client21, ModTime: 0));

        Assert.False(result.Accepted);
        Assert.Equal(LevelEntryStopPoint.MissingLevel, result.StopPoint);
        Assert.Equal(["missing.nw"], levels.RequestedNames);
        Assert.Equal(new byte[] { 47, (byte)'m', (byte)'i', (byte)'s', (byte)'s', (byte)'i', (byte)'n', (byte)'g', (byte)'.', (byte)'n', (byte)'w', 10 }, session.TakeOutboundBytes());
    }

    [Fact]
    public void BeginSetLevelQueuesPlayerWarpForModernSingleLevelWhenModTimeIsZero()
    {
        var session = ReadyForLevelWarpSession();
        var levels = new MemoryLevelLookup();
        levels.Add(new LevelEntrySnapshot("start.nw"));

        var result = WarpWorldEntryBoundary.BeginSetLevel(
            session,
            levels,
            new LevelWarpRequest("start.nw", X: 30.5f, Y: 31.25f, Z: 0, ClientVersionId.Client21, ModTime: 0));

        Assert.True(result.Accepted);
        Assert.Equal(LevelEntryStopPoint.BeforeSendLevelRuntime, result.StopPoint);
        Assert.Equal(SessionLifecycle.ReadyForLevelRuntime, session.Lifecycle);
        Assert.Equal(new byte[] { 46, 93, 94, (byte)'s', (byte)'t', (byte)'a', (byte)'r', (byte)'t', (byte)'.', (byte)'n', (byte)'w', 10 }, session.TakeOutboundBytes());
    }

    [Fact]
    public void BeginSetLevelQueuesPlayerWarp2ForModernGmapWhenModTimeIsZero()
    {
        var session = ReadyForLevelWarpSession();
        var levels = new MemoryLevelLookup();
        levels.Add(new LevelEntrySnapshot(
            "world_a01.nw",
            new LevelMapSnapshot(LevelMapType.Gmap, "world.gmap"),
            MapX: 4,
            MapY: 5));

        var result = WarpWorldEntryBoundary.BeginSetLevel(
            session,
            levels,
            new LevelWarpRequest("world_a01.nw", X: 30.5f, Y: 31.25f, Z: 1.5f, ClientVersionId.Client21, ModTime: 0));

        Assert.True(result.Accepted);
        Assert.Equal(LevelEntryStopPoint.BeforeSendLevelRuntime, result.StopPoint);
        Assert.Equal(new byte[] { 81, 93, 94, 85, 36, 37, (byte)'w', (byte)'o', (byte)'r', (byte)'l', (byte)'d', (byte)'.', (byte)'g', (byte)'m', (byte)'a', (byte)'p', 10 }, session.TakeOutboundBytes());
    }

    [Fact]
    public void BeginSetLevelDoesNotQueueWarpPacketForModernClientWhenModTimeIsNonZero()
    {
        var session = ReadyForLevelWarpSession();
        var levels = new MemoryLevelLookup();
        levels.Add(new LevelEntrySnapshot("start.nw"));

        var result = WarpWorldEntryBoundary.BeginSetLevel(
            session,
            levels,
            new LevelWarpRequest("start.nw", X: 30.5f, Y: 31.25f, Z: 0, ClientVersionId.Client21, ModTime: 123));

        Assert.True(result.Accepted);
        Assert.Equal(LevelEntryStopPoint.BeforeSendLevelRuntime, result.StopPoint);
        Assert.Empty(session.TakeOutboundBytes());
    }

    [Fact]
    public void BeginSetLevelQueuesPlayerWarpForOldClientEvenWhenModTimeIsNonZero()
    {
        var session = ReadyForLevelWarpSession();
        var levels = new MemoryLevelLookup();
        levels.Add(new LevelEntrySnapshot("start.nw"));

        var result = WarpWorldEntryBoundary.BeginSetLevel(
            session,
            levels,
            new LevelWarpRequest("start.nw", X: 30.5f, Y: 31.25f, Z: 0, ClientVersionId.Client1411, ModTime: 123));

        Assert.True(result.Accepted);
        Assert.Equal(LevelEntryStopPoint.BeforeSendLevelRuntime, result.StopPoint);
        Assert.Equal(new byte[] { 46, 93, 94, (byte)'s', (byte)'t', (byte)'a', (byte)'r', (byte)'t', (byte)'.', (byte)'n', (byte)'w', 10 }, session.TakeOutboundBytes());
    }

    [Fact]
    public void BeginClientLevelWarpPacketParsesInboundPacketAndPreservesCurrentZ()
    {
        var session = ReadyForLevelWarpSession();
        var levels = new MemoryLevelLookup();
        levels.Add(new LevelEntrySnapshot(
            "world_a01.nw",
            new LevelMapSnapshot(LevelMapType.Gmap, "world.gmap"),
            MapX: 4,
            MapY: 5));
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)PlayerToServerPacketId.LevelWarp);
        packet.WriteGChar(61);
        packet.WriteGChar(62);
        packet.WriteBytes("world_a01.nw"u8);

        var result = WarpWorldEntryBoundary.BeginClientLevelWarpPacket(
            session,
            levels,
            new PlayerWarpState(new LevelEntrySnapshot("start.nw"), CurrentX: 12.0f, CurrentY: 13.0f),
            packet.ToArray(),
            ClientVersionId.Client21,
            currentZ: 1.5f,
            PlayerWarpSettings.Default);

        Assert.True(result.CppReturnValue);
        Assert.Equal(PlayerWarpStopPoint.TargetReadyForSendLevelRuntime, result.StopPoint);
        Assert.Equal(
            new byte[] { 81, 93, 94, 85, 36, 37, (byte)'w', (byte)'o', (byte)'r', (byte)'l', (byte)'d', (byte)'.', (byte)'g', (byte)'m', (byte)'a', (byte)'p', 10 },
            session.TakeOutboundBytes());
    }

    private static ClientSessionSkeleton ReadyForLevelWarpSession()
    {
        var session = new ClientSessionSkeleton(7);
        var packet = new GraalBinaryWriter();
        packet.WriteGChar(5);
        packet.WriteGChar(42);
        packet.WriteBytes("G3D0311C"u8);
        packet.WriteGChar(4);
        packet.WriteBytes("Ruan"u8);
        packet.WriteGChar(2);
        packet.WriteBytes("pw"u8);
        packet.WriteBytes("win"u8);
        Assert.True(session.ReceiveLoginPacket(packet.ToArray()));
        Assert.True(session.ReceiveServerListAuthResponse(
            new ServerListVerifyAccount2Response("pc:Ruan", 7, PlayerSessionType.Client3, "SUCCESS")));
        Assert.True(PlayerSendLoginContinuation.Begin(
            session,
            new PlayerSendLoginAccount("pc:Ruan", false, "", false, false, true, ["0.0.0.0"], false),
            new PlayerSendLoginOptions(false, "Graal Reborn", [])).Accepted);
        _ = session.TakeOutboundBytes();

        PostLoginWorldEntryBoundary.BeginClient(session, BaseSnapshot());
        _ = session.TakeOutboundBytes();
        return session;
    }

    private static PostLoginPlayerSnapshot BaseSnapshot()
    {
        var prop = new GraalBinaryWriter();
        prop.WriteGChar(0);

        return new PostLoginPlayerSnapshot(
            PlayerId: 7,
            Type: PlayerSessionType.Client3,
            AccountNameProperty: prop.ToArray(),
            NicknameProperty: prop.ToArray(),
            CurrentLevelProperty: prop.ToArray(),
            XProperty: [64],
            YProperty: [65],
            AlignmentProperty: [66],
            IpAddressProperty: [32, 32, 32, 32, 33],
            LoginPropertySource: new PlayerPropertySource(
                Nickname: "Ruan",
                MaxPower: 3,
                Hitpoints: 4,
                Rupees: 0,
                Arrows: 0,
                Bombs: 0,
                GlovePower: 0,
                SwordPower: 0,
                SwordImage: "",
                ShieldPower: 0,
                ShieldImage: "",
                Gani: "",
                HeadImage: "",
                ChatMessage: "",
                Colors: [0, 0, 0, 0, 0],
                PlayerId: 7,
                X: 0,
                Y: 0,
                Sprite: 0,
                Status: 0,
                CarrySprite: 0,
                CurrentLevel: "start.nw",
                HorseImage: "",
                HorseBombCount: 0,
                CarryNpcId: 0,
                ApCounter: 0,
                MagicPoints: 0,
                Kills: 0,
                Deaths: 0,
                OnlineSeconds: 0,
                AccountIp: 1,
                Alignment: 0,
                AdditionalFlags: 0,
                AccountName: "pc:Ruan",
                BodyImage: "",
                EloRating: 0,
                EloDeviation: 0,
                GaniAttributes: new Dictionary<int, string>(),
                Os: "",
                TextCodePage: 0,
                CommunityName: "Ruan"),
            LoginPropertyIds: [],
            PlayerFlags: [],
            ServerFlags: []);
    }

    private sealed class MemoryLevelLookup : ILevelLookup
    {
        private readonly Dictionary<string, LevelEntrySnapshot> _levels = new(StringComparer.OrdinalIgnoreCase);

        public List<string> RequestedNames { get; } = [];

        public LevelEntrySnapshot? FindLevel(string levelName)
        {
            RequestedNames.Add(levelName);
            return _levels.GetValueOrDefault(levelName);
        }

        public void Add(LevelEntrySnapshot level) =>
            _levels[level.LevelName] = level;
    }
}

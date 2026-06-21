using Preagonal.GServer.Game;
using Preagonal.GServer.Network;
using Preagonal.GServer.Protocol;
using Xunit;

namespace Preagonal.GServer.Network.Tests;

public sealed class SendLevelBoundaryTests
{
    [Fact]
    public void BeginModernQueuesLevelNameModTimeLinksAndSignsWhenCacheEmptyAndBoardIsCurrent()
    {
        var session = ReadyForLevelRuntimeSession();
        var level = new ModernLevelPayload(
            LevelName: "start.nw",
            LevelModTime: 1,
            BoardPacket: EmptyBoardPacket(),
            Layers: [],
            LinksPacket: "links\n"u8.ToArray(),
            SignsPacket: "signs\n"u8.ToArray());

        var result = SendLevelBoundary.BeginModern(
            session,
            level,
            new SendLevelRequest(RequestedModTime: 1, CachedLevelModTime: 0, FromAdjacent: false));

        Assert.True(result.Accepted);
        Assert.Equal(SendLevelStopPoint.BeforeGmapCorrection, result.StopPoint);
        Assert.Equal(SessionLifecycle.DynamicLevelPayloadSent, session.Lifecycle);
        Assert.Equal(
            new byte[]
            {
                38, (byte)'s', (byte)'t', (byte)'a', (byte)'r', (byte)'t', (byte)'.', (byte)'n', (byte)'w', 10,
                71, 32, 32, 32, 32, 33, 10,
                (byte)'l', (byte)'i', (byte)'n', (byte)'k', (byte)'s', 10,
                (byte)'s', (byte)'i', (byte)'g', (byte)'n', (byte)'s', 10,
                32, 10
            },
            session.TakeOutboundBytes());
    }

    [Fact]
    public void BeginModernQueuesRawBoardAndLayersBeforeModTimeWhenClientNeedsLevelPayload()
    {
        var session = ReadyForLevelRuntimeSession();
        var board = EmptyBoardPacket();
        var layer = new byte[] { 35, 2, 0, 0, 64, 64, 10 };
        var level = new ModernLevelPayload(
            LevelName: "start.nw",
            LevelModTime: 1,
            BoardPacket: board,
            Layers: [new LevelLayerPayload(1, layer)],
            LinksPacket: [],
            SignsPacket: []);

        var result = SendLevelBoundary.BeginModern(
            session,
            level,
            new SendLevelRequest(RequestedModTime: 0, CachedLevelModTime: 0, FromAdjacent: false));

        Assert.True(result.Accepted);
        var bytes = session.TakeOutboundBytes();
        Assert.Equal(
            new byte[] { 38, (byte)'s', (byte)'t', (byte)'a', (byte)'r', (byte)'t', (byte)'.', (byte)'n', (byte)'w', 10, 132, 32, 96, 34, 10 },
            bytes[..15]);
        Assert.Equal(board, bytes[15..(15 + board.Length)]);
        var layerHeaderStart = 15 + board.Length;
        Assert.Equal(new byte[] { 132, 32, 32, 39, 10 }, bytes[layerHeaderStart..(layerHeaderStart + 5)]);
        Assert.Equal(layer, bytes[(layerHeaderStart + 5)..(layerHeaderStart + 5 + layer.Length)]);
        Assert.Equal(new byte[] { 71, 32, 32, 32, 32, 33, 10, 32, 10 }, bytes[^9..]);
    }

    [Fact]
    public void BeginModernCanUseParsedNwBoardPacketForRawLevelPayload()
    {
        var session = ReadyForLevelRuntimeSession();
        var parsed = NwLevelParser.Parse("""
            GLEVNW01
            BOARD 0 0 2 0 AB+/
            """);
        var board = NwLevelPacketBuilder.BuildBoardPacket(parsed.Level);
        var level = new ModernLevelPayload(
            LevelName: "start.nw",
            LevelModTime: 1,
            BoardPacket: board,
            Layers: [],
            LinksPacket: [],
            SignsPacket: []);

        SendLevelBoundary.BeginModern(
            session,
            level,
            new SendLevelRequest(RequestedModTime: 0, CachedLevelModTime: 0, FromAdjacent: false));

        var bytes = session.TakeOutboundBytes();
        Assert.Equal(new byte[] { 132, 32, 96, 34, 10 }, bytes[10..15]);
        Assert.Equal([133, 1, 0, 191, 15], bytes[15..20]);
    }

    [Fact]
    public void BeginModernCanUseParsedNwStaticPacketsThroughChests()
    {
        var session = ReadyForLevelRuntimeSession();
        var parsed = NwLevelParser.Parse(
            """
            GLEVNW01
            BOARD 0 0 1 0 AB
            LINK next.nw 1 2 3 4 5 6
            SIGN 4 5
            A
            SIGNEND
            CHEST 10 11 redrupee 3
            """,
            linkTargetExists: levelName => levelName == "next.nw");
        var level = new ModernLevelPayload(
            LevelName: "start.nw",
            LevelModTime: 1,
            BoardPacket: NwLevelPacketBuilder.BuildBoardPacket(parsed.Level),
            Layers: [],
            LinksPacket: NwLevelPacketBuilder.BuildLinksPacket(parsed.Level),
            SignsPacket: NwLevelPacketBuilder.BuildSignsPacket(parsed.Level),
            Chests:
            [
                ..parsed.Level.Chests.Select(chest => new LevelChestPayload(
                    HasChest: false,
                    X: (byte)chest.X,
                    Y: (byte)chest.Y,
                    ItemIndex: (byte)chest.ItemType,
                    SignIndex: (byte)chest.SignIndex))
            ]);

        SendLevelBoundary.BeginModern(
            session,
            level,
            new SendLevelRequest(RequestedModTime: 1, CachedLevelModTime: 0, FromAdjacent: false));

        Assert.Equal(
            new byte[]
            {
                38, (byte)'s', (byte)'t', (byte)'a', (byte)'r', (byte)'t', (byte)'.', (byte)'n', (byte)'w', 10,
                71, 32, 32, 32, 32, 33, 10,
                33, (byte)'n', (byte)'e', (byte)'x', (byte)'t', (byte)'.', (byte)'n', (byte)'w',
                (byte)' ', (byte)'1', (byte)' ', (byte)'2', (byte)' ', (byte)'3', (byte)' ', (byte)'4',
                (byte)' ', (byte)'5', (byte)' ', (byte)'6', 10,
                37, 36, 37, 32, 128, 10,
                32, 10,
                36, 32, 42, 43, 34, 35, 10
            },
            session.TakeOutboundBytes());
    }

    [Fact]
    public void BeginModernCanUseFilesystemLoadedNwStaticPayload()
    {
        using var temp = new TemporaryDirectory();
        var world = Directory.CreateDirectory(Path.Combine(temp.Path, "world"));
        File.WriteAllText(
            Path.Combine(world.FullName, "start.nw"),
            """
            GLEVNW01
            BOARD 0 0 1 0 AB
            LINK next.nw 1 2 3 4 5 6
            SIGN 4 5
            A
            SIGNEND
            CHEST 10 11 redrupee 3
            """);

        var fileSystem = new IndexedServerFileSystem(temp.Path);
        fileSystem.AddDirectory("world", "*.nw");
        var loaded = new NwLevelFileLoader(fileSystem)
            .TryLoad("start.nw", linkTargetExists: levelName => levelName == "next.nw");
        var level = ModernLevelPayload.FromNwStatic(loaded.ToModernStaticPayload(playerHasChest: _ => false));
        var session = ReadyForLevelRuntimeSession();

        SendLevelBoundary.BeginModern(
            session,
            level,
            new SendLevelRequest(RequestedModTime: loaded.ModTime, CachedLevelModTime: 0, FromAdjacent: false));

        var modTime = new GraalBinaryWriter();
        modTime.WriteGChar((byte)ServerToPlayerPacketId.LevelModTime);
        modTime.WriteGInt5(unchecked((uint)loaded.ModTime));

        Assert.Equal(
            new byte[] {
                38, (byte)'s', (byte)'t', (byte)'a', (byte)'r', (byte)'t', (byte)'.', (byte)'n', (byte)'w', 10,
            }
            .Concat(modTime.ToArray())
            .Concat(new byte[] {
                10,
                33, (byte)'n', (byte)'e', (byte)'x', (byte)'t', (byte)'.', (byte)'n', (byte)'w',
                (byte)' ', (byte)'1', (byte)' ', (byte)'2', (byte)' ', (byte)'3', (byte)' ', (byte)'4',
                (byte)' ', (byte)'5', (byte)' ', (byte)'6', 10,
                37, 36, 37, 32, 128, 10,
                32, 10,
                36, 32, 42, 43, 34, 35, 10
            }).ToArray(),
            session.TakeOutboundBytes());
    }

    [Fact]
    public void BeginModernUsesLevelModTimeWhenRequestedModTimeIsMinusOne()
    {
        var session = ReadyForLevelRuntimeSession();
        var level = new ModernLevelPayload(
            LevelName: "start.nw",
            LevelModTime: 1,
            BoardPacket: EmptyBoardPacket(),
            Layers: [],
            LinksPacket: [],
            SignsPacket: []);

        SendLevelBoundary.BeginModern(
            session,
            level,
            new SendLevelRequest(RequestedModTime: -1, CachedLevelModTime: 0, FromAdjacent: false));

        Assert.Equal(
            new byte[]
            {
                38, (byte)'s', (byte)'t', (byte)'a', (byte)'r', (byte)'t', (byte)'.', (byte)'n', (byte)'w', 10,
                71, 32, 32, 32, 32, 33, 10,
                32, 10
            },
            session.TakeOutboundBytes());
    }

    [Fact]
    public void BeginModernSkipsDynamicLevelPacketsWhenWarpCameFromAdjacentLevel()
    {
        var session = ReadyForLevelRuntimeSession();
        var level = new ModernLevelPayload(
            LevelName: "start.nw",
            LevelModTime: 1,
            BoardPacket: EmptyBoardPacket(),
            Layers: [],
            LinksPacket: [],
            SignsPacket: [],
            BoardChanges: [new LevelBoardChangePayload(1, [1, 2, 3])],
            Chests: [new LevelChestPayload(false, 10, 11, 2, 3)],
            Horses: [new LevelHorsePayload("horse"u8.ToArray())],
            Baddies: [new LevelBaddyPayload(5, [70, 71])]);

        var result = SendLevelBoundary.BeginModern(
            session,
            level,
            new SendLevelRequest(RequestedModTime: 0, CachedLevelModTime: 1, FromAdjacent: true));

        Assert.True(result.Accepted);
        Assert.Equal(SendLevelStopPoint.BeforeGmapCorrection, result.StopPoint);
        Assert.Equal(
            new byte[] { 38, (byte)'s', (byte)'t', (byte)'a', (byte)'r', (byte)'t', (byte)'.', (byte)'n', (byte)'w', 10 },
            session.TakeOutboundBytes());
    }

    [Fact]
    public void BeginModernFiltersBoardChangesByCachedLevelModTime()
    {
        var session = ReadyForLevelRuntimeSession();
        var level = new ModernLevelPayload(
            LevelName: "start.nw",
            LevelModTime: 1,
            BoardPacket: EmptyBoardPacket(),
            Layers: [],
            LinksPacket: [],
            SignsPacket: [],
            BoardChanges:
            [
                new LevelBoardChangePayload(9, [1]),
                new LevelBoardChangePayload(10, BoardChangePayload(1, 2, 3, 4, [80, 81])),
                new LevelBoardChangePayload(11, BoardChangePayload(5, 6, 7, 8, [82]))
            ]);

        SendLevelBoundary.BeginModern(
            session,
            level,
            new SendLevelRequest(RequestedModTime: 0, CachedLevelModTime: 10, FromAdjacent: false));

        Assert.Equal(
            new byte[]
            {
                38, (byte)'s', (byte)'t', (byte)'a', (byte)'r', (byte)'t', (byte)'.', (byte)'n', (byte)'w', 10,
                32, 33, 34, 35, 36, 80, 81, 37, 38, 39, 40, 82, 10
            },
            session.TakeOutboundBytes());
    }

    [Fact]
    public void BeginModernQueuesChestPacketsWithOwnedAndUnownedBranches()
    {
        var session = ReadyForLevelRuntimeSession();
        var level = new ModernLevelPayload(
            LevelName: "start.nw",
            LevelModTime: 1,
            BoardPacket: EmptyBoardPacket(),
            Layers: [],
            LinksPacket: [],
            SignsPacket: [],
            Chests:
            [
                new LevelChestPayload(false, 10, 11, 2, 3),
                new LevelChestPayload(true, 12, 13, 4, 5)
            ]);

        SendLevelBoundary.BeginModern(
            session,
            level,
            new SendLevelRequest(RequestedModTime: 0, CachedLevelModTime: 1, FromAdjacent: false));

        Assert.Equal(
            new byte[]
            {
                38, (byte)'s', (byte)'t', (byte)'a', (byte)'r', (byte)'t', (byte)'.', (byte)'n', (byte)'w', 10,
                32, 10,
                36, 32, 42, 43, 34, 35, 10,
                36, 33, 44, 45, 10
            },
            session.TakeOutboundBytes());
    }

    [Fact]
    public void BeginModernQueuesHorseAndBaddyPacketsAfterChests()
    {
        var session = ReadyForLevelRuntimeSession();
        var level = new ModernLevelPayload(
            LevelName: "start.nw",
            LevelModTime: 1,
            BoardPacket: EmptyBoardPacket(),
            Layers: [],
            LinksPacket: [],
            SignsPacket: [],
            Horses: [new LevelHorsePayload("horse"u8.ToArray())],
            Baddies: [new LevelBaddyPayload(5, [70, 71])]);

        SendLevelBoundary.BeginModern(
            session,
            level,
            new SendLevelRequest(RequestedModTime: 0, CachedLevelModTime: 1, FromAdjacent: false));

        Assert.Equal(
            new byte[]
            {
                38, (byte)'s', (byte)'t', (byte)'a', (byte)'r', (byte)'t', (byte)'.', (byte)'n', (byte)'w', 10,
                32, 10,
                49, (byte)'h', (byte)'o', (byte)'r', (byte)'s', (byte)'e', 10,
                34, 37, 70, 71, 10
            },
            session.TakeOutboundBytes());
    }

    [Fact]
    public void BeginModernQueuesPostDynamicNonGmapPacketsBeforeNearbyPlayerProps()
    {
        var session = ReadyForLevelRuntimeSession();
        var level = new ModernLevelPayload(
            LevelName: "start.nw",
            LevelModTime: 1,
            BoardPacket: EmptyBoardPacket(),
            Layers: [],
            LinksPacket: [],
            SignsPacket: [],
            RuntimeContinuation: new LevelRuntimeContinuationPayload(
                GmapName: null,
                HasMapContext: false,
                IsLevelLeader: false,
                IsSingleplayer: false,
                NewWorldTime: 1,
                NpcsPacket: [70, 10]));

        var result = SendLevelBoundary.BeginModern(
            session,
            level,
            new SendLevelRequest(RequestedModTime: 0, CachedLevelModTime: 1, FromAdjacent: false));

        Assert.True(result.Accepted);
        Assert.Equal(SendLevelStopPoint.BeforeNearbyPlayerProps, result.StopPoint);
        Assert.Equal(SessionLifecycle.LevelRuntimePacketsSent, session.Lifecycle);
        Assert.Equal(
            new byte[]
            {
                38, (byte)'s', (byte)'t', (byte)'a', (byte)'r', (byte)'t', (byte)'.', (byte)'n', (byte)'w', 10,
                32, 10,
                206, 32, 10,
                74, 32, 32, 32, 33, 10,
                188, (byte)'s', (byte)'t', (byte)'a', (byte)'r', (byte)'t', (byte)'.', (byte)'n', (byte)'w', 10,
                70, 10
            },
            session.TakeOutboundBytes());
    }

    [Fact]
    public void BeginModernQueuesGmapCorrectionAndLeaderWhenAdjacentWithMapContext()
    {
        var session = ReadyForLevelRuntimeSession();
        var level = new ModernLevelPayload(
            LevelName: "inside.nw",
            LevelModTime: 1,
            BoardPacket: EmptyBoardPacket(),
            Layers: [],
            LinksPacket: [],
            SignsPacket: [],
            RuntimeContinuation: new LevelRuntimeContinuationPayload(
                GmapName: "world.gmap",
                HasMapContext: true,
                IsLevelLeader: true,
                IsSingleplayer: false,
                NewWorldTime: 1,
                NpcsPacket: []));

        SendLevelBoundary.BeginModern(
            session,
            level,
            new SendLevelRequest(RequestedModTime: 0, CachedLevelModTime: 1, FromAdjacent: true));

        Assert.Equal(
            new byte[]
            {
                38, (byte)'i', (byte)'n', (byte)'s', (byte)'i', (byte)'d', (byte)'e', (byte)'.', (byte)'n', (byte)'w', 10,
                38, (byte)'w', (byte)'o', (byte)'r', (byte)'l', (byte)'d', (byte)'.', (byte)'g', (byte)'m', (byte)'a', (byte)'p', 10,
                206, 32, 10,
                42, 10,
                74, 32, 32, 32, 33, 10,
                188, (byte)'w', (byte)'o', (byte)'r', (byte)'l', (byte)'d', (byte)'.', (byte)'g', (byte)'m', (byte)'a', (byte)'p', 10
            },
            session.TakeOutboundBytes());
    }

    [Fact]
    public void BeginModernQueuesNearbySameLevelPropsAndReturnsSelfPropBroadcastsWithoutMap()
    {
        var session = ReadyForLevelRuntimeSession();
        var level = new ModernLevelPayload(
            LevelName: "start.nw",
            LevelModTime: 1,
            BoardPacket: EmptyBoardPacket(),
            Layers: [],
            LinksPacket: [],
            SignsPacket: [],
            RuntimeContinuation: RuntimeContinuation(),
            PlayerSync: new LevelEntryPlayerSyncPayload(
                IsSingleplayer: false,
                HasMapContext: false,
                IsGroupMap: false,
                MapKey: null,
                PlayerGroup: null,
                PlayerMapX: 0,
                PlayerMapY: 0,
                SelfPropsPacket: [1],
                NearbyPlayers:
                [
                    new NearbyLevelPlayerSnapshot(7, true, true, null, null, 0, 0, [99]),
                    new NearbyLevelPlayerSnapshot(8, true, true, null, null, 0, 0, [65]),
                    new NearbyLevelPlayerSnapshot(9, false, true, null, null, 0, 0, [66]),
                    new NearbyLevelPlayerSnapshot(10, true, false, null, null, 0, 0, [67])
                ]));

        var result = SendLevelBoundary.BeginModern(
            session,
            level,
            new SendLevelRequest(RequestedModTime: 0, CachedLevelModTime: 1, FromAdjacent: false));

        Assert.Equal(SendLevelStopPoint.BeforeRuntimeSimulation, result.StopPoint);
        Assert.Equal(SessionLifecycle.LevelEntryPlayerPropsSynchronized, session.Lifecycle);
        var broadcast = Assert.Single(result.Broadcasts!);
        Assert.Equal(8, broadcast.PlayerId);
        Assert.Equal(new byte[] { 1, 10 }, broadcast.Packet);
        AssertEndsWith(new byte[] { 65, 10, 66, 10 }, session.TakeOutboundBytes());
    }

    [Fact]
    public void BeginModernFiltersNearbyPropsByGmapAreaAndGroup()
    {
        var session = ReadyForLevelRuntimeSession();
        var level = new ModernLevelPayload(
            LevelName: "inside.nw",
            LevelModTime: 1,
            BoardPacket: EmptyBoardPacket(),
            Layers: [],
            LinksPacket: [],
            SignsPacket: [],
            RuntimeContinuation: RuntimeContinuation("world.gmap"),
            PlayerSync: new LevelEntryPlayerSyncPayload(
                IsSingleplayer: false,
                HasMapContext: true,
                IsGroupMap: true,
                MapKey: "world.gmap",
                PlayerGroup: "red",
                PlayerMapX: 4,
                PlayerMapY: 4,
                SelfPropsPacket: [1],
                NearbyPlayers:
                [
                    new NearbyLevelPlayerSnapshot(8, true, false, "world.gmap", "red", 5, 4, [65]),
                    new NearbyLevelPlayerSnapshot(9, true, false, "world.gmap", "blue", 5, 4, [66]),
                    new NearbyLevelPlayerSnapshot(10, true, false, "world.gmap", "red", 6, 4, [67]),
                    new NearbyLevelPlayerSnapshot(11, true, false, "other.gmap", "red", 4, 4, [68])
                ]));

        var result = SendLevelBoundary.BeginModern(
            session,
            level,
            new SendLevelRequest(RequestedModTime: 0, CachedLevelModTime: 1, FromAdjacent: false));

        var broadcast = Assert.Single(result.Broadcasts!);
        Assert.Equal(8, broadcast.PlayerId);
        Assert.Equal(new byte[] { 1, 10 }, broadcast.Packet);
        AssertEndsWith(new byte[] { 65, 10 }, session.TakeOutboundBytes());
    }

    [Fact]
    public void BeginModernSkipsNearbyPropsForSingleplayerLevel()
    {
        var session = ReadyForLevelRuntimeSession();
        var level = new ModernLevelPayload(
            LevelName: "start.nw",
            LevelModTime: 1,
            BoardPacket: EmptyBoardPacket(),
            Layers: [],
            LinksPacket: [],
            SignsPacket: [],
            RuntimeContinuation: RuntimeContinuation(),
            PlayerSync: new LevelEntryPlayerSyncPayload(
                IsSingleplayer: true,
                HasMapContext: false,
                IsGroupMap: false,
                MapKey: null,
                PlayerGroup: null,
                PlayerMapX: 0,
                PlayerMapY: 0,
                SelfPropsPacket: [1],
                NearbyPlayers: [new NearbyLevelPlayerSnapshot(8, true, true, null, null, 0, 0, [65])]));

        var result = SendLevelBoundary.BeginModern(
            session,
            level,
            new SendLevelRequest(RequestedModTime: 0, CachedLevelModTime: 1, FromAdjacent: false));

        Assert.Empty(result.Broadcasts!);
        Assert.DoesNotContain((byte)65, session.TakeOutboundBytes());
    }

    [Fact]
    public void BeginModernSkipsStaticLevelPayloadWhenCachedModTimeIsKnown()
    {
        var session = ReadyForLevelRuntimeSession();
        var level = new ModernLevelPayload(
            LevelName: "start.nw",
            LevelModTime: 1,
            BoardPacket: EmptyBoardPacket(),
            Layers: [],
            LinksPacket: "links\n"u8.ToArray(),
            SignsPacket: "signs\n"u8.ToArray());

        SendLevelBoundary.BeginModern(
            session,
            level,
            new SendLevelRequest(RequestedModTime: 0, CachedLevelModTime: 1, FromAdjacent: false));

        Assert.Equal(
            new byte[] { 38, (byte)'s', (byte)'t', (byte)'a', (byte)'r', (byte)'t', (byte)'.', (byte)'n', (byte)'w', 10, 32, 10 },
            session.TakeOutboundBytes());
    }

    private static ClientSessionSkeleton ReadyForLevelRuntimeSession()
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

        var levels = new MemoryLevelLookup();
        levels.Add(new LevelEntrySnapshot("start.nw"));
        var result = WarpWorldEntryBoundary.BeginSetLevel(
            session,
            levels,
            new LevelWarpRequest("start.nw", 30, 30, 0, ClientVersionId.Client21, 123));
        Assert.True(result.Accepted);
        _ = session.TakeOutboundBytes();
        return session;
    }

    private static byte[] EmptyBoardPacket()
    {
        var board = new byte[1 + 64 * 64 * 2 + 1];
        board[0] = 133;
        board[^1] = 10;
        return board;
    }

    private static LevelRuntimeContinuationPayload RuntimeContinuation(string? gmapName = null) =>
        new(
            GmapName: gmapName,
            HasMapContext: gmapName is not null,
            IsLevelLeader: false,
            IsSingleplayer: false,
            NewWorldTime: 1,
            NpcsPacket: []);

    private static byte[] BoardChangePayload(byte x, byte y, byte width, byte height, byte[] tiles)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar(x);
        writer.WriteGChar(y);
        writer.WriteGChar(width);
        writer.WriteGChar(height);
        writer.WriteBytes(tiles);
        return writer.ToArray();
    }

    private static void AssertEndsWith(byte[] expectedSuffix, byte[] actual)
    {
        Assert.True(actual.Length >= expectedSuffix.Length);
        Assert.Equal(expectedSuffix, actual[^expectedSuffix.Length..]);
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

        public LevelEntrySnapshot? FindLevel(string levelName) =>
            _levels.GetValueOrDefault(levelName);

        public void Add(LevelEntrySnapshot level) =>
            _levels[level.LevelName] = level;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}

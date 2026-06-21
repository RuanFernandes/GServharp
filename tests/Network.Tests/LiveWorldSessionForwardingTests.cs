using Preagonal.GServer.Game;
using Preagonal.GServer.Network;
using Preagonal.GServer.Protocol;
using Xunit;

namespace Preagonal.GServer.Network.Tests;

public sealed class LiveWorldSessionForwardingTests
{
    [Fact]
    public void ForwardConfirmedLevelAreaPacketDeliversInLevelMembershipOrder()
    {
        var server = new RuntimeServer();
        var level = new RuntimeLevel("start.nw");
        var sender = Add(server, 7, RuntimePlayerKind.Client, level);
        Add(server, 8, RuntimePlayerKind.Client, level);
        Add(server, 9, RuntimePlayerKind.RemoteControl, level);
        Add(server, 10, RuntimePlayerKind.Client, level);
        var sinks = CreateSinks(7, 8, 9, 10);

        var deliveries = LiveWorldSessionForwarder.ForwardConfirmedLevelAreaPacket(
            server,
            sender,
            [1, 2, 3],
            AsSinks(sinks));

        Assert.Equal([8, 10], deliveries.Select(delivery => delivery.PlayerId));
        Assert.Equal([1, 2, 3], sinks[8].Packets.Single());
        Assert.Empty(sinks[9].Packets);
        Assert.Equal([1, 2, 3], sinks[10].Packets.Single());
    }

    [Fact]
    public void ApplyAndForwardConfirmedPlayerPropsMutatesSenderAndDeliversForwardedMovementBytes()
    {
        var server = new RuntimeServer();
        var level = new RuntimeLevel("start.nw");
        var sender = Add(server, 7, RuntimePlayerKind.Client, level);
        var recipient = Add(server, 8, RuntimePlayerKind.Client, level);
        recipient.ClientVersion = ClientVersionId.Client23;
        var sinks = CreateSinks(7, 8);
        var updates = new[]
        {
            IncomingPlayerPropertyUpdate.GShort(PlayerPropertyId.X2, 1120),
            IncomingPlayerPropertyUpdate.GShort(PlayerPropertyId.Y2, 1120)
        };

        var deliveries = LiveWorldSessionForwarder.ApplyAndForwardConfirmedPlayerProps(
            server,
            sender,
            updates,
            senderSupportsPreciseMovement: true,
            AsSinks(sinks));

        Assert.Equal(560, sender.PixelX);
        Assert.Equal(560, sender.PixelY);
        var delivery = Assert.Single(deliveries);
        Assert.Equal(8, delivery.PlayerId);
        Assert.Equal(
            new byte[]
            {
                40, 32, 39,
                47, 102,
                48, 102,
                110, 40, 128,
                111, 40, 128,
                10
            },
            sinks[8].Packets.Single());
    }

    [Fact]
    public void MovementUsesRecipientVersion()
    {
        var server = new RuntimeServer();
        var level = new RuntimeLevel("start.nw");
        var sender = Add(server, 7, RuntimePlayerKind.Client, level);
        sender.ClientVersion = ClientVersionId.Client6037;
        var oldClient = Add(server, 8, RuntimePlayerKind.Client, level);
        oldClient.ClientVersion = ClientVersionId.Client222;
        var newClient = Add(server, 9, RuntimePlayerKind.Client, level);
        newClient.ClientVersion = ClientVersionId.Client6037;
        var sinks = CreateSinks(7, 8, 9);
        var updates = new[]
        {
            IncomingPlayerPropertyUpdate.GShort(PlayerPropertyId.X2, 1120)
        };

        var deliveries = LiveWorldSessionForwarder.ApplyAndForwardConfirmedPlayerProps(
            server,
            sender,
            updates,
            senderSupportsPreciseMovement: true,
            AsSinks(sinks));

        Assert.Equal([8, 9], deliveries.Select(delivery => delivery.PlayerId));
        Assert.Equal((byte)PlayerPropertyId.X2 + 32, sinks[8].Packets.Single()[3]);
        Assert.Equal((byte)PlayerPropertyId.X + 32, sinks[9].Packets.Single()[3]);
    }

    [Fact]
    public void ApplyAndForwardConfirmedCurrentPowerUsesPostMutationStateLikeCpp()
    {
        var server = new RuntimeServer();
        var level = new RuntimeLevel("start.nw");
        var sender = Add(server, 7, RuntimePlayerKind.Client, level);
        sender.Alignment = 39;
        sender.Hitpoints = 2.0f;
        sender.MaxPower = 10;
        Add(server, 8, RuntimePlayerKind.Client, level);
        var sinks = CreateSinks(7, 8);

        var deliveries = LiveWorldSessionForwarder.ApplyAndForwardConfirmedPlayerProps(
            server,
            sender,
            [IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.CurrentPower, 8)],
            senderSupportsPreciseMovement: true,
            AsSinks(sinks));

        Assert.Equal(2.0f, sender.Hitpoints);
        var delivery = Assert.Single(deliveries);
        Assert.Equal(8, delivery.PlayerId);
        Assert.Equal([40, 32, 39, 34, 36, 10], sinks[8].Packets.Single());
    }

    [Fact]
    public void ApplyAndForwardConfirmedCurrentLevelUsesSingleplayerSuffixLikeCpp()
    {
        var server = new RuntimeServer();
        var level = new RuntimeLevel("start.nw") { IsSingleplayer = true };
        var sender = Add(server, 7, RuntimePlayerKind.Client, level);
        Add(server, 8, RuntimePlayerKind.Client, level);
        var sinks = CreateSinks(7, 8);

        var deliveries = LiveWorldSessionForwarder.ApplyAndForwardConfirmedPlayerProps(
            server,
            sender,
            [IncomingPlayerPropertyUpdate.String(PlayerPropertyId.CurrentLevel, "start.nw")],
            senderSupportsPreciseMovement: true,
            AsSinks(sinks));

        Assert.Equal("start.nw", sender.CurrentLevelName);
        var delivery = Assert.Single(deliveries);
        Assert.Equal(8, delivery.PlayerId);
        Assert.Equal(
            [40, 32, 39, 52, 53, 115, 116, 97, 114, 116, 46, 110, 119, 46, 115, 105, 110, 103, 108, 101, 112, 108, 97, 121, 101, 114, 10],
            sinks[8].Packets.Single());
    }

    [Fact]
    public void ApplyAndForwardConfirmedAccountNameUsesRuntimeAccountNameLikeCpp()
    {
        var server = new RuntimeServer();
        var level = new RuntimeLevel("start.nw");
        var sender = Add(server, 7, RuntimePlayerKind.Client, level);
        Add(server, 8, RuntimePlayerKind.Client, level);
        var sinks = CreateSinks(7, 8);

        var deliveries = LiveWorldSessionForwarder.ApplyAndForwardConfirmedPlayerProps(
            server,
            sender,
            [IncomingPlayerPropertyUpdate.NoValue(PlayerPropertyId.AccountName)],
            senderSupportsPreciseMovement: true,
            AsSinks(sinks));

        Assert.Equal("pc:7", sender.AccountName);
        var delivery = Assert.Single(deliveries);
        Assert.Equal(8, delivery.PlayerId);
        Assert.Equal(
            [40, 32, 39, 66, 36, 112, 99, 58, 55, 10],
            sinks[8].Packets.Single());
    }

    [Fact]
    public void ApplyAndForwardConfirmedIpAddressUsesRuntimeAccountIpLikeCpp()
    {
        var server = new RuntimeServer();
        var level = new RuntimeLevel("start.nw");
        var sender = Add(server, 7, RuntimePlayerKind.Client, level);
        sender.AccountIp = 1;
        Add(server, 8, RuntimePlayerKind.Client, level);
        var sinks = CreateSinks(7, 8);

        var deliveries = LiveWorldSessionForwarder.ApplyAndForwardConfirmedPlayerProps(
            server,
            sender,
            [IncomingPlayerPropertyUpdate.NoValue(PlayerPropertyId.IpAddress)],
            senderSupportsPreciseMovement: true,
            AsSinks(sinks));

        var delivery = Assert.Single(deliveries);
        Assert.Equal(8, delivery.PlayerId);
        Assert.Equal(
            [40, 32, 39, 62, 32, 32, 32, 32, 33, 10],
            sinks[8].Packets.Single());
    }

    [Fact]
    public void ApplyAndForwardConfirmedCommunityNameUsesRuntimeCommunityNameLikeCpp()
    {
        var server = new RuntimeServer();
        var level = new RuntimeLevel("start.nw");
        var sender = Add(server, 7, RuntimePlayerKind.Client, level);
        sender.CommunityName = "Ruan";
        Add(server, 8, RuntimePlayerKind.Client, level);
        var sinks = CreateSinks(7, 8);

        var deliveries = LiveWorldSessionForwarder.ApplyAndForwardConfirmedPlayerProps(
            server,
            sender,
            [IncomingPlayerPropertyUpdate.NoValue(PlayerPropertyId.CommunityName)],
            senderSupportsPreciseMovement: true,
            AsSinks(sinks));

        var delivery = Assert.Single(deliveries);
        Assert.Equal(8, delivery.PlayerId);
        Assert.Equal(
            [40, 32, 39, 114, 36, 82, 117, 97, 110, 10],
            sinks[8].Packets.Single());
    }

    [Fact]
    public void ApplyAndForwardConfirmedRatingUsesRuntimeRatingLikeCpp()
    {
        var server = new RuntimeServer();
        var level = new RuntimeLevel("start.nw");
        var sender = Add(server, 7, RuntimePlayerKind.Client, level);
        sender.EloRating = 1500;
        sender.EloDeviation = 50;
        Add(server, 8, RuntimePlayerKind.Client, level);
        var sinks = CreateSinks(7, 8);

        var deliveries = LiveWorldSessionForwarder.ApplyAndForwardConfirmedPlayerProps(
            server,
            sender,
            [IncomingPlayerPropertyUpdate.NoValue(PlayerPropertyId.Rating)],
            senderSupportsPreciseMovement: true,
            AsSinks(sinks));

        var expected = new GraalBinaryWriter();
        expected.WriteGChar((byte)ServerToPlayerPacketId.OtherPlayerProps);
        expected.WriteGShort(7);
        expected.WriteGChar((byte)PlayerPropertyId.Rating);
        expected.WriteGInt((uint)(((1500 & 0xFFF) << 9) | (50 & 0x1FF)));
        expected.WriteByte((byte)'\n');

        var delivery = Assert.Single(deliveries);
        Assert.Equal(8, delivery.PlayerId);
        Assert.Equal(expected.ToArray(), sinks[8].Packets.Single());
    }

    [Fact]
    public void ApplyAndForwardConfirmedPlayerStatusMessageSendsDirectGlobalBroadcastAndGenericLocalTail()
    {
        var server = new RuntimeServer();
        var level = new RuntimeLevel("start.nw");
        var sender = Add(server, 7, RuntimePlayerKind.Client, level);
        Add(server, 8, RuntimePlayerKind.Client, level);
        Add(server, 9, RuntimePlayerKind.RemoteControl, level);
        Add(server, 10, RuntimePlayerKind.Client, new RuntimeLevel("other.nw"));
        var sinks = CreateSinks(7, 8, 9, 10);

        var deliveries = LiveWorldSessionForwarder.ApplyAndForwardConfirmedPlayerProps(
            server,
            sender,
            [IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.PlayerStatusMessage, 4)],
            senderSupportsPreciseMovement: true,
            AsSinks(sinks));

        var expected = new GraalBinaryWriter();
        expected.WriteGChar((byte)ServerToPlayerPacketId.OtherPlayerProps);
        expected.WriteGShort(7);
        expected.WriteGChar((byte)PlayerPropertyId.PlayerStatusMessage);
        expected.WriteGChar(4);
        expected.WriteByte((byte)'\n');

        Assert.Equal(4, sender.StatusMessage);
        Assert.Equal([8, 10, 8], deliveries.Select(delivery => delivery.PlayerId));
        Assert.Equal([expected.ToArray(), expected.ToArray()], sinks[8].Packets);
        Assert.Empty(sinks[9].Packets);
        Assert.Equal(expected.ToArray(), sinks[10].Packets.Single());
    }

    [Fact]
    public void ApplyAndForwardConfirmedUdpPortSendsDirectGlobalBroadcastAndGenericLocalTail()
    {
        var server = new RuntimeServer();
        var level = new RuntimeLevel("start.nw");
        var sender = Add(server, 7, RuntimePlayerKind.Client, level);
        Add(server, 8, RuntimePlayerKind.Client, level);
        Add(server, 10, RuntimePlayerKind.Client, new RuntimeLevel("other.nw"));
        var sinks = CreateSinks(7, 8, 10);

        var deliveries = LiveWorldSessionForwarder.ApplyAndForwardConfirmedPlayerProps(
            server,
            sender,
            [IncomingPlayerPropertyUpdate.GInt(PlayerPropertyId.UdpPort, 14900)],
            senderSupportsPreciseMovement: true,
            AsSinks(sinks));

        var expected = new GraalBinaryWriter();
        expected.WriteGChar((byte)ServerToPlayerPacketId.OtherPlayerProps);
        expected.WriteGShort(7);
        expected.WriteGChar((byte)PlayerPropertyId.UdpPort);
        expected.WriteGInt(14900);
        expected.WriteByte((byte)'\n');

        Assert.Equal(14900u, sender.UdpPort);
        Assert.Equal([8, 10, 8], deliveries.Select(delivery => delivery.PlayerId));
        Assert.Equal([expected.ToArray(), expected.ToArray()], sinks[8].Packets);
        Assert.Equal(expected.ToArray(), sinks[10].Packets.Single());
    }

    [Fact]
    public void ApplyAndForwardConfirmedNicknameSendsExpectedPacketTypesToPeersAndNotNpcServer()
    {
        var server = new RuntimeServer();
        var level = new RuntimeLevel("start.nw");
        var sender = Add(server, 7, RuntimePlayerKind.Client, level);
        Add(server, 8, RuntimePlayerKind.Client, level);
        Add(server, 9, RuntimePlayerKind.RemoteControl, level);
        Add(server, 10, RuntimePlayerKind.NpcServer, level);
        Add(server, 11, RuntimePlayerKind.Client, new RuntimeLevel("other.nw"));
        var sinks = CreateSinks(7, 8, 9, 10, 11);

        var deliveries = LiveWorldSessionForwarder.ApplyAndForwardConfirmedPlayerProps(
            server,
            sender,
            [IncomingPlayerPropertyUpdate.String(PlayerPropertyId.Nickname, "Ruan")],
            senderSupportsPreciseMovement: true,
            AsSinks(sinks),
            RuntimePlayerPropsOptions.Default with
            {
                NicknamePolicy = RuntimeNicknameUpdatePolicy.WordFilterAllowedNoGuild
            });

        var expected = new GraalBinaryWriter();
        expected.WriteGChar((byte)ServerToPlayerPacketId.OtherPlayerProps);
        expected.WriteGShort(7);
        expected.WriteGChar((byte)PlayerPropertyId.Nickname);
        expected.WriteGChar(4);
        expected.WriteBytes("Ruan"u8);
        expected.WriteByte((byte)'\n');

        Assert.Equal("Ruan", sender.Nickname);
        Assert.Equal([8, 9, 11, 7], deliveries.Select(delivery => delivery.PlayerId));
        Assert.Equal(expected.ToArray(), sinks[8].Packets.Single());
        Assert.Equal(expected.ToArray(), sinks[9].Packets.Single());
        Assert.Empty(sinks[10].Packets);
        Assert.Equal(expected.ToArray(), sinks[11].Packets.Single());
    }

    [Fact]
    public void ApplyAndForwardConfirmedNicknameAlsoSendsSelfPlayerPropsPacket()
    {
        var server = new RuntimeServer();
        var level = new RuntimeLevel("start.nw");
        var sender = Add(server, 7, RuntimePlayerKind.Client, level);
        Add(server, 8, RuntimePlayerKind.Client, level);
        Add(server, 9, RuntimePlayerKind.RemoteControl, level);
        Add(server, 10, RuntimePlayerKind.NpcServer, level);
        Add(server, 11, RuntimePlayerKind.Client, new RuntimeLevel("other.nw"));
        var sinks = CreateSinks(7, 8, 9, 10, 11);

        var deliveries = LiveWorldSessionForwarder.ApplyAndForwardConfirmedPlayerProps(
            server,
            sender,
            [IncomingPlayerPropertyUpdate.String(PlayerPropertyId.Nickname, "Ruan")],
            senderSupportsPreciseMovement: true,
            AsSinks(sinks),
            RuntimePlayerPropsOptions.Default with
            {
                NicknamePolicy = RuntimeNicknameUpdatePolicy.WordFilterAllowedNoGuild
            });

        var expectedGlobal = new GraalBinaryWriter();
        expectedGlobal.WriteGChar((byte)ServerToPlayerPacketId.OtherPlayerProps);
        expectedGlobal.WriteGShort(7);
        expectedGlobal.WriteGChar((byte)PlayerPropertyId.Nickname);
        expectedGlobal.WriteGChar(4);
        expectedGlobal.WriteBytes("Ruan"u8);
        expectedGlobal.WriteByte((byte)'\n');

        var expectedSelf = new GraalBinaryWriter();
        expectedSelf.WriteGChar((byte)ServerToPlayerPacketId.PlayerProps);
        expectedSelf.WriteGChar((byte)PlayerPropertyId.Nickname);
        expectedSelf.WriteGChar(4);
        expectedSelf.WriteBytes("Ruan"u8);
        expectedSelf.WriteByte((byte)'\n');

        Assert.Equal("Ruan", sender.Nickname);
        Assert.Equal([8, 9, 11, 7], deliveries.Select(delivery => delivery.PlayerId));
        Assert.Equal(expectedGlobal.ToArray(), sinks[8].Packets.Single());
        Assert.Equal(expectedGlobal.ToArray(), sinks[9].Packets.Single());
        Assert.Equal(expectedGlobal.ToArray(), sinks[11].Packets.Single());
        Assert.Empty(sinks[10].Packets);
        Assert.Equal(expectedSelf.ToArray(), sinks[7].Packets.Single());
    }
    [Fact]
    public void TryApplyAndForwardPlayerPropsBlocksParsedButUnportedSideEffectsWithoutForwarding()
    {
        var server = new RuntimeServer();
        var level = new RuntimeLevel("start.nw");
        var sender = Add(server, 7, RuntimePlayerKind.Client, level);
        Add(server, 8, RuntimePlayerKind.Client, level);
        var sinks = CreateSinks(7, 8);

        var result = LiveWorldSessionForwarder.TryApplyAndForwardConfirmedPlayerProps(
            server,
            sender,
            [
                IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.X, 70),
                IncomingPlayerPropertyUpdate.String(PlayerPropertyId.Nickname, "Ruan")
            ],
            senderSupportsPreciseMovement: false,
            AsSinks(sinks));

        Assert.Equal(LiveWorldPlayerPropsForwardingStatus.Blocked, result.Status);
        Assert.Contains("PLPROP_NICKNAME", result.Message, StringComparison.Ordinal);
        Assert.Equal(560, sender.PixelX);
        Assert.Empty(result.Deliveries);
        Assert.Empty(sinks[8].Packets);
    }

    [Fact]
    public void ForwardConfirmedLevelAreaPacketFiltersByGmapGroupAndDistance()
    {
        var server = new RuntimeServer();
        var map = new RuntimeMap("world.gmap", RuntimeMapType.Gmap, IsGroupMap: true);
        var sender = Add(server, 7, RuntimePlayerKind.Client, new RuntimeLevel("inside.nw") { Map = map });
        sender.Group = "red";
        sender.MapX = 4;
        sender.MapY = 4;

        var nearby = Add(server, 8, RuntimePlayerKind.Client, new RuntimeLevel("near.nw") { Map = map });
        nearby.Group = "red";
        nearby.MapX = 5;
        nearby.MapY = 4;

        var wrongGroup = Add(server, 9, RuntimePlayerKind.Client, new RuntimeLevel("group.nw") { Map = map });
        wrongGroup.Group = "blue";
        wrongGroup.MapX = 5;
        wrongGroup.MapY = 4;

        var sinks = CreateSinks(7, 8, 9);

        var deliveries = LiveWorldSessionForwarder.ForwardConfirmedLevelAreaPacket(
            server,
            sender,
            [70],
            AsSinks(sinks));

        var delivery = Assert.Single(deliveries);
        Assert.Equal(8, delivery.PlayerId);
        Assert.Equal([70], sinks[8].Packets.Single());
        Assert.Empty(sinks[9].Packets);
    }

    [Fact]
    public void ForwardConfirmedOneLevelPacketUsesLevelMembershipOrderAndIgnoresMapArea()
    {
        var server = new RuntimeServer();
        var map = new RuntimeMap("world.gmap", RuntimeMapType.Gmap, IsGroupMap: true);
        var level = new RuntimeLevel("inside.nw") { Map = map };
        var sender = Add(server, 7, RuntimePlayerKind.Client, level);
        sender.Group = "red";
        sender.MapX = 4;
        sender.MapY = 4;

        var sameLevelFarAway = Add(server, 8, RuntimePlayerKind.Client, level);
        sameLevelFarAway.Group = "blue";
        sameLevelFarAway.MapX = 20;
        sameLevelFarAway.MapY = 20;

        Add(server, 9, RuntimePlayerKind.RemoteControl, level);
        Add(server, 10, RuntimePlayerKind.Client, level);
        Add(server, 11, RuntimePlayerKind.Client, new RuntimeLevel("other.nw") { Map = map });
        var sinks = CreateSinks(7, 8, 9, 10, 11);

        var deliveries = LiveWorldSessionForwarder.ForwardConfirmedOneLevelPacket(
            server,
            level,
            [80, 81],
            AsSinks(sinks),
            new HashSet<ushort> { sender.Id, 10 });

        var delivery = Assert.Single(deliveries);
        Assert.Equal(8, delivery.PlayerId);
        Assert.Equal([80, 81], sinks[8].Packets.Single());
        Assert.Empty(sinks[9].Packets);
        Assert.Empty(sinks[10].Packets);
        Assert.Empty(sinks[11].Packets);
    }

    [Fact]
    public void ForwardingHelpersDoNotBlanketFilterHiddenClients()
    {
        var server = new RuntimeServer();
        var level = new RuntimeLevel("start.nw");
        var sender = Add(server, 7, RuntimePlayerKind.Client, level);
        var hidden = Add(server, 8, RuntimePlayerKind.Client, level);
        hidden.IsHiddenClient = true;
        var sinks = CreateSinks(7, 8);

        var deliveries = LiveWorldSessionForwarder.ForwardConfirmedOneLevelPacket(
            server,
            level,
            [90],
            AsSinks(sinks),
            new HashSet<ushort> { sender.Id });

        var delivery = Assert.Single(deliveries);
        Assert.Equal(8, delivery.PlayerId);
        Assert.Equal([90], sinks[8].Packets.Single());
    }

    [Fact]
    public void ForwardingIncludesDeletedPlayersUntilCleanupRuns()
    {
        var server = new RuntimeServer();
        var level = new RuntimeLevel("start.nw");
        var sender = Add(server, 7, RuntimePlayerKind.Client, level);
        var deleted = Add(server, 8, RuntimePlayerKind.Client, level);
        var sinks = CreateSinks(7, 8);

        server.DeletePlayer(deleted);
        var beforeCleanup = LiveWorldSessionForwarder.ForwardConfirmedOneLevelPacket(
            server,
            level,
            [91],
            AsSinks(sinks),
            new HashSet<ushort> { sender.Id });

        server.CleanupDeletedPlayers();
        var afterCleanup = LiveWorldSessionForwarder.ForwardConfirmedOneLevelPacket(
            server,
            level,
            [92],
            AsSinks(sinks),
            new HashSet<ushort> { sender.Id });

        var delivery = Assert.Single(beforeCleanup);
        Assert.Equal(8, delivery.PlayerId);
        Assert.Equal([91], sinks[8].Packets.Single());
        Assert.Empty(afterCleanup);
        Assert.DoesNotContain((ushort)8, level.PlayerIds);
    }

    private static RuntimePlayer Add(RuntimeServer server, ushort id, RuntimePlayerKind kind, RuntimeLevel level)
    {
        var player = new RuntimePlayer(id, $"pc:{id}", kind);
        server.AddPlayer(player, id);
        player.JoinLevel(level);
        return player;
    }

    private static IReadOnlyDictionary<ushort, MemoryLiveWorldSessionSink> CreateSinks(params ushort[] playerIds) =>
        playerIds.ToDictionary(id => id, id => new MemoryLiveWorldSessionSink(id));

    private static IReadOnlyDictionary<ushort, ILiveWorldSessionSink> AsSinks(
        IReadOnlyDictionary<ushort, MemoryLiveWorldSessionSink> sinks) =>
        sinks.ToDictionary(entry => entry.Key, entry => (ILiveWorldSessionSink)entry.Value);

    private sealed class MemoryLiveWorldSessionSink(ushort playerId) : ILiveWorldSessionSink
    {
        public ushort PlayerId { get; } = playerId;
        public List<byte[]> Packets { get; } = [];

        public void QueuePacket(byte[] packet) =>
            Packets.Add(packet);
    }
}


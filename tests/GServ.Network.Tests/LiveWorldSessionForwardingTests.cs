using GServ.Game;
using GServ.Network;
using GServ.Protocol;
using Xunit;

namespace GServ.Network.Tests;

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
        Add(server, 8, RuntimePlayerKind.Client, level);
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

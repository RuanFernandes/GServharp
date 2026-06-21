using Preagonal.GServer.Game;

namespace Preagonal.GServer.Game.Tests;

public sealed class LevelEntryVisibilitySelectionTests
{
    [Fact]
    public void SelectForLevelEntryUsesLevelPlayerOrderWhenThereIsNoMap()
    {
        var server = new RuntimeServer();
        var level = new RuntimeLevel("start.nw");
        var joining = Add(server, 7, RuntimePlayerKind.Client, level);
        Add(server, 8, RuntimePlayerKind.Client, level);
        Add(server, 9, RuntimePlayerKind.RemoteControl, level);
        Add(server, 10, RuntimePlayerKind.Client, new RuntimeLevel("elsewhere.nw"));

        var selection = LevelEntryVisibilitySelector.Select(server, joining);

        Assert.Equal([8], selection.BroadcastSelfPropsToPlayerIds);
        Assert.Equal([8, 9], selection.SendOtherPropsFromPlayerIds);
    }

    [Fact]
    public void SelectForLevelEntrySkipsEverythingForSingleplayerLevel()
    {
        var server = new RuntimeServer();
        var level = new RuntimeLevel("single.nw") { IsSingleplayer = true };
        var joining = Add(server, 7, RuntimePlayerKind.Client, level);
        Add(server, 8, RuntimePlayerKind.Client, level);

        var selection = LevelEntryVisibilitySelector.Select(server, joining);

        Assert.Empty(selection.BroadcastSelfPropsToPlayerIds);
        Assert.Empty(selection.SendOtherPropsFromPlayerIds);
    }

    [Fact]
    public void SelectForLevelEntryFiltersMapPlayersByClientMapGroupAndDistance()
    {
        var server = new RuntimeServer();
        var map = new RuntimeMap("world.gmap", RuntimeMapType.Gmap, IsGroupMap: true);
        var joiningLevel = new RuntimeLevel("inside.nw") { Map = map };
        var joining = Add(server, 7, RuntimePlayerKind.Client, joiningLevel);
        joining.Group = "red";
        joining.MapX = 4;
        joining.MapY = 4;

        var nearby = Add(server, 8, RuntimePlayerKind.Client, new RuntimeLevel("near.nw") { Map = map });
        nearby.Group = "red";
        nearby.MapX = 5;
        nearby.MapY = 4;

        var wrongGroup = Add(server, 9, RuntimePlayerKind.Client, new RuntimeLevel("group.nw") { Map = map });
        wrongGroup.Group = "blue";
        wrongGroup.MapX = 5;
        wrongGroup.MapY = 4;

        var tooFar = Add(server, 10, RuntimePlayerKind.Client, new RuntimeLevel("far.nw") { Map = map });
        tooFar.Group = "red";
        tooFar.MapX = 6;
        tooFar.MapY = 4;

        var otherMap = Add(server, 11, RuntimePlayerKind.Client, new RuntimeLevel("other.nw") { Map = new RuntimeMap("other.gmap", RuntimeMapType.Gmap) });
        otherMap.Group = "red";
        otherMap.MapX = 4;
        otherMap.MapY = 4;

        var nonClient = Add(server, 12, RuntimePlayerKind.RemoteControl, new RuntimeLevel("rc.nw") { Map = map });
        nonClient.Group = "red";
        nonClient.MapX = 4;
        nonClient.MapY = 4;

        var selection = LevelEntryVisibilitySelector.Select(server, joining);

        Assert.Equal([8], selection.BroadcastSelfPropsToPlayerIds);
        Assert.Equal([8], selection.SendOtherPropsFromPlayerIds);
    }

    [Fact]
    public void SelectLevelAreaRecipientsUsesLevelOrderAndSkipsNonClientsWithoutMap()
    {
        var server = new RuntimeServer();
        var level = new RuntimeLevel("start.nw");
        var sender = Add(server, 7, RuntimePlayerKind.Client, level);
        Add(server, 8, RuntimePlayerKind.Client, level);
        Add(server, 9, RuntimePlayerKind.RemoteControl, level);
        Add(server, 10, RuntimePlayerKind.Client, level);

        var recipients = LiveWorldForwardingSelector.SelectLevelAreaRecipients(
            server,
            sender,
            new HashSet<ushort> { sender.Id });

        Assert.Equal([8, 10], recipients);
    }

    [Fact]
    public void SelectLevelAreaRecipientsFiltersBySameMapGroupAndDistance()
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

        var tooFar = Add(server, 10, RuntimePlayerKind.Client, new RuntimeLevel("far.nw") { Map = map });
        tooFar.Group = "red";
        tooFar.MapX = 6;
        tooFar.MapY = 4;

        var recipients = LiveWorldForwardingSelector.SelectLevelAreaRecipients(
            server,
            sender,
            new HashSet<ushort> { sender.Id });

        Assert.Equal([8], recipients);
    }

    private static RuntimePlayer Add(RuntimeServer server, ushort id, RuntimePlayerKind kind, RuntimeLevel level)
    {
        var player = new RuntimePlayer(id, $"pc:{id}", kind);
        server.AddPlayer(player, id);
        player.JoinLevel(level);
        return player;
    }
}

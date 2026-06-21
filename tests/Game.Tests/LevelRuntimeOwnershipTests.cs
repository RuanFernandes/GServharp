using Preagonal.GServer.Game;

namespace Preagonal.GServer.Game.Tests;

public sealed class LevelRuntimeOwnershipTests
{
    [Fact]
    public void LevelAddPlayerAppendsIdsAndReturnsZeroBasedIndex()
    {
        var level = new RuntimeLevel("start.nw");

        Assert.Equal(0, level.AddPlayer(7));
        Assert.Equal(1, level.AddPlayer(8));

        Assert.Equal([7, 8], level.PlayerIds);
        Assert.True(level.IsPlayerLeader(7));
        Assert.False(level.IsPlayerLeader(8));
    }

    [Fact]
    public void LevelRemovePlayerErasesAllMatchingIdsAndPromotesFrontLeader()
    {
        var level = new RuntimeLevel("start.nw");
        level.AddPlayer(7);
        level.AddPlayer(8);
        level.AddPlayer(7);

        level.RemovePlayer(7);

        Assert.Equal([8], level.PlayerIds);
        Assert.True(level.IsPlayerLeader(8));
    }

    [Fact]
    public void ServerAddPlayerAssignsRequestedIdAndOverwritesExistingEntryForSameId()
    {
        var server = new RuntimeServer();
        var first = new RuntimePlayer(0, "pc:First", RuntimePlayerKind.Client);
        var replacement = new RuntimePlayer(0, "pc:Replacement", RuntimePlayerKind.Client);

        Assert.True(server.AddPlayer(first, 7));
        Assert.True(server.AddPlayer(replacement, 7));

        Assert.Equal(7, first.Id);
        Assert.Equal(7, replacement.Id);
        Assert.Same(replacement, server.GetPlayer(7));
        Assert.Equal([7], server.PlayerIds);
    }

    [Fact]
    public void ServerAddPlayerWithoutRequestedIdStartsAtTwoAndReusesSmallestFreedId()
    {
        var server = new RuntimeServer();
        var first = new RuntimePlayer(0, "pc:First", RuntimePlayerKind.Client);
        var second = new RuntimePlayer(0, "pc:Second", RuntimePlayerKind.Client);

        Assert.True(server.AddPlayer(first));
        Assert.True(server.AddPlayer(second));

        Assert.Equal(2, first.Id);
        Assert.Equal(3, second.Id);

        server.DeletePlayer(first);
        server.CleanupDeletedPlayers();

        var third = new RuntimePlayer(0, "pc:Third", RuntimePlayerKind.Client);
        Assert.True(server.AddPlayer(third));
        Assert.Equal(2, third.Id);
    }

    [Fact]
    public void ServerDeletePlayerDefersRemovalUntilCleanup()
    {
        var server = new RuntimeServer();
        var player = new RuntimePlayer(0, "pc:Ruan", RuntimePlayerKind.Client);
        server.AddPlayer(player, 7);

        Assert.True(server.DeletePlayer(player));
        Assert.Same(player, server.GetPlayer(7));

        server.CleanupDeletedPlayers();

        Assert.Null(server.GetPlayer(7));
        Assert.Empty(server.PlayerIds);
    }

    [Fact]
    public void WarpBetweenLevelsLeavesPreviousLevelBeforeJoiningNextLevel()
    {
        var first = new RuntimeLevel("first.nw");
        var second = new RuntimeLevel("second.nw");
        var player = new RuntimePlayer(7, "pc:Ruan", RuntimePlayerKind.Client);

        player.JoinLevel(first);
        player.JoinLevel(second);

        Assert.Empty(first.PlayerIds);
        Assert.Equal([7], second.PlayerIds);
        Assert.Same(second, player.Level);
    }
}

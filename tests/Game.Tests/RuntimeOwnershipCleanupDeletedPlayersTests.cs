using Preagonal.GServer.Game;
using Xunit;

namespace Preagonal.GServer.Game.Tests;

public sealed class RuntimeOwnershipCleanupDeletedPlayersTests
{
    [Fact]
    public void CleanupDeletedPlayers_RemovesPlayersWithoutScriptReferences()
    {
        var server = new RuntimeServer();
        var player = new RuntimePlayer(0, "pc:test", RuntimePlayerKind.Client);
        server.AddPlayer(player, 2);
        Assert.NotNull(server.GetPlayer(2));

        var deleted = server.DeletePlayer(player);
        Assert.True(deleted);

        server.CleanupDeletedPlayers(isScriptObjectReferenced: null);
        Assert.Null(server.GetPlayer(2));
    }

    [Fact]
    public void CleanupDeletedPlayersSkipsPlayersReportedAsScriptReferenced()
    {
        var server = new RuntimeServer();
        var player = new RuntimePlayer(0, "pc:test", RuntimePlayerKind.Client);
        server.AddPlayer(player, 2);
        server.DeletePlayer(player);

        var observed = false;
        server.CleanupDeletedPlayers(isScriptObjectReferenced: p =>
        {
            observed = p.Id == 2;
            return true;
        });

        Assert.Equal(2, Assert.Single(server.PlayerIds));
        Assert.True(observed);
        Assert.NotNull(server.GetPlayer(2));
    }

    [Fact]
    public void CleanupDeletedPlayersCallsBeforeDeleteCallbackForConfirmedDeletion()
    {
        var server = new RuntimeServer();
        var player = new RuntimePlayer(0, "pc:test", RuntimePlayerKind.Client);
        server.AddPlayer(player, 2);
        server.DeletePlayer(player);

        var deletedPlayer = false;
        server.CleanupDeletedPlayers(
            isScriptObjectReferenced: null,
            onScriptObjectReferenced: _ => throw new Xunit.Sdk.XunitException("Should not be called."),
            onBeforeDelete: p =>
            {
                Assert.Equal(2, p.Id);
                deletedPlayer = true;
            });

        Assert.Null(server.GetPlayer(2));
        Assert.True(deletedPlayer);
    }

    [Fact]
    public void CleanupDeletedPlayersCallsScriptReferenceCallbackOnlyWhenScriptObjectStillReferenced()
    {
        var server = new RuntimeServer();
        var player = new RuntimePlayer(0, "pc:test", RuntimePlayerKind.Client);
        server.AddPlayer(player, 2);
        server.DeletePlayer(player);

        var scriptReferencedCalled = false;
        server.CleanupDeletedPlayers(
            isScriptObjectReferenced: _ => true,
            onScriptObjectReferenced: _ => scriptReferencedCalled = true,
            onBeforeDelete: _ => throw new Xunit.Sdk.XunitException("Should not delete while script-referenced."));

        Assert.True(scriptReferencedCalled);
        Assert.NotNull(server.GetPlayer(2));
    }
}

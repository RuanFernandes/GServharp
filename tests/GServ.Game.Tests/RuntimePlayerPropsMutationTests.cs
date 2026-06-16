using GServ.Game;
using GServ.Protocol;
using Xunit;

namespace GServ.Game.Tests;

public sealed class RuntimePlayerPropsMutationTests
{
    [Fact]
    public void AppliesConfirmedLegacyMovementPropsToRuntimePlayerState()
    {
        var player = new RuntimePlayer(7, "pc:Ruan", RuntimePlayerKind.Client);
        var updates = new[]
        {
            IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.X, 70),
            IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.Y, 71),
            IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.Z, 55),
            IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.Sprite, 2)
        };

        RuntimePlayerPropsApplier.ApplyConfirmed(player, updates);

        Assert.Equal(560, player.PixelX);
        Assert.Equal(568, player.PixelY);
        Assert.Equal(40, player.PixelZ);
        Assert.Equal(2, player.Sprite);
        Assert.True(player.MovementUpdated);
        Assert.True(player.TouchTestRequested);
    }

    [Fact]
    public void AppliesConfirmedPreciseMovementAndStringProps()
    {
        var player = new RuntimePlayer(7, "pc:Ruan", RuntimePlayerKind.Client);
        var updates = new[]
        {
            IncomingPlayerPropertyUpdate.GShort(PlayerPropertyId.X2, 1120),
            IncomingPlayerPropertyUpdate.GShort(PlayerPropertyId.Y2, 1121),
            IncomingPlayerPropertyUpdate.String(PlayerPropertyId.CurrentLevel, "start.nw"),
            IncomingPlayerPropertyUpdate.String(PlayerPropertyId.Gani, "walk")
        };

        RuntimePlayerPropsApplier.ApplyConfirmed(player, updates);

        Assert.Equal(560, player.PixelX);
        Assert.Equal(-560, player.PixelY);
        Assert.Equal("start.nw", player.CurrentLevelName);
        Assert.Equal("walk", player.Gani);
        Assert.True(player.MovementUpdated);
        Assert.True(player.TouchTestRequested);
    }
}

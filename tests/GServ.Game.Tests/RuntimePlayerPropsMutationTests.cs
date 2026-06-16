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
            IncomingPlayerPropertyUpdate.GShort(PlayerPropertyId.Z2, 79),
            IncomingPlayerPropertyUpdate.String(PlayerPropertyId.CurrentLevel, "start.nw"),
            IncomingPlayerPropertyUpdate.String(PlayerPropertyId.Gani, "walk")
        };

        RuntimePlayerPropsApplier.ApplyConfirmed(player, updates);

        Assert.Equal(560, player.PixelX);
        Assert.Equal(-560, player.PixelY);
        Assert.Equal(-39, player.PixelZ);
        Assert.Equal("start.nw", player.CurrentLevelName);
        Assert.Equal("walk", player.Gani);
        Assert.True(player.MovementUpdated);
        Assert.True(player.TouchTestRequested);
    }

    [Fact]
    public void IgnoresConfirmedReadOnlyAndNoByteProps()
    {
        var player = new RuntimePlayer(7, "pc:Ruan", RuntimePlayerKind.Client);
        var updates = new[]
        {
            IncomingPlayerPropertyUpdate.NoValue(PlayerPropertyId.Id),
            IncomingPlayerPropertyUpdate.NoValue(PlayerPropertyId.KillsCount),
            IncomingPlayerPropertyUpdate.NoValue(PlayerPropertyId.DeathsCount),
            IncomingPlayerPropertyUpdate.NoValue(PlayerPropertyId.OnlineSeconds),
            IncomingPlayerPropertyUpdate.NoValue(PlayerPropertyId.AccountName),
            IncomingPlayerPropertyUpdate.NoValue(PlayerPropertyId.Rating),
            IncomingPlayerPropertyUpdate.NoValue(PlayerPropertyId.JoinLeaveLevel),
            IncomingPlayerPropertyUpdate.NoValue(PlayerPropertyId.PlayerConnected),
            IncomingPlayerPropertyUpdate.NoValue(PlayerPropertyId.Unknown81)
        };

        RuntimePlayerPropsApplier.ApplyConfirmed(player, updates);

        Assert.Equal(0, player.PixelX);
        Assert.Equal(0, player.PixelY);
        Assert.Equal(0, player.PixelZ);
        Assert.False(player.MovementUpdated);
        Assert.False(player.TouchTestRequested);
    }

    [Fact]
    public void AppliesConfirmedScalarInventoryAndStatPropsWithCppClamps()
    {
        var player = new RuntimePlayer(7, "pc:Ruan", RuntimePlayerKind.Client)
        {
            Alignment = 35,
            HeartLimit = 20,
            Hitpoints = 4,
            MaxPower = 10
        };
        var updates = new[]
        {
            IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.MaxPower, 15),
            IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.CurrentPower, 40),
            IncomingPlayerPropertyUpdate.GInt(PlayerPropertyId.RupeesCount, 12_000_000),
            IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.ArrowsCount, 150),
            IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.BombsCount, 151),
            IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.GlovePower, 9),
            IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.BombPower, 8),
            IncomingPlayerPropertyUpdate.GShort(PlayerPropertyId.ApCounter, 123),
            IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.MagicPoints, 200),
            IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.AdditionalFlags, 77),
            IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.Alignment, 120),
            IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.CarrySprite, 12),
            IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.HorseBushes, 6)
        };

        RuntimePlayerPropsApplier.ApplyConfirmed(player, updates);

        Assert.Equal(15, player.MaxPower);
        Assert.Equal(15, player.Hitpoints);
        Assert.Equal(9_999_999, player.Rupees);
        Assert.Equal(99, player.Arrows);
        Assert.Equal(99, player.Bombs);
        Assert.Equal(3, player.GlovePower);
        Assert.Equal(3, player.BombPower);
        Assert.Equal(123, player.ApCounter);
        Assert.Equal(100, player.MagicPoints);
        Assert.Equal(77, player.AdditionalFlags);
        Assert.Equal(100, player.Alignment);
        Assert.Equal(12, player.CarrySprite);
        Assert.Equal(6, player.HorseBombCount);
        Assert.False(player.MovementUpdated);
        Assert.False(player.TouchTestRequested);
    }

    [Fact]
    public void CurrentPowerIncreaseIsIgnoredWhenAlignmentIsBelowFortyLikeCpp()
    {
        var player = new RuntimePlayer(7, "pc:Ruan", RuntimePlayerKind.Client)
        {
            Alignment = 39,
            Hitpoints = 2,
            MaxPower = 10
        };

        RuntimePlayerPropsApplier.ApplyConfirmed(
            player,
            [IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.CurrentPower, 8)]);

        Assert.Equal(2, player.Hitpoints);
    }

    [Fact]
    public void AppliesConfirmedEnvironmentAndGaniAttributeProps()
    {
        var player = new RuntimePlayer(7, "pc:Ruan", RuntimePlayerKind.Client);
        var updates = new[]
        {
            IncomingPlayerPropertyUpdate.String(PlayerPropertyId.PlayerLanguage, "pt"),
            IncomingPlayerPropertyUpdate.String(PlayerPropertyId.OsType, "wind"),
            IncomingPlayerPropertyUpdate.GInt(PlayerPropertyId.TextCodePage, 1252),
            IncomingPlayerPropertyUpdate.String(PlayerPropertyId.GAttrib1, "sword"),
            IncomingPlayerPropertyUpdate.String(PlayerPropertyId.GAttrib30, "tail")
        };

        RuntimePlayerPropsApplier.ApplyConfirmed(player, updates);

        Assert.Equal("pt", player.Language);
        Assert.Equal("wind", player.Os);
        Assert.Equal(1252u, player.TextCodePage);
        Assert.Equal("sword", player.GaniAttributes[0]);
        Assert.Equal("tail", player.GaniAttributes[29]);
        Assert.False(player.MovementUpdated);
        Assert.False(player.TouchTestRequested);
    }

    [Fact]
    public void AppliesConfirmedColorPropWithoutMovementSideEffects()
    {
        var player = new RuntimePlayer(7, "pc:Ruan", RuntimePlayerKind.Client);

        RuntimePlayerPropsApplier.ApplyConfirmed(
            player,
            [IncomingPlayerPropertyUpdate.Bytes(PlayerPropertyId.Colors, [1, 2, 3, 4, 5])]);

        Assert.Equal([1, 2, 3, 4, 5], player.Colors);
        Assert.False(player.MovementUpdated);
        Assert.False(player.TouchTestRequested);
    }

    [Fact]
    public void AppliesConfirmedBodyImagePropWithCppLengthLimit()
    {
        var player = new RuntimePlayer(7, "pc:Ruan", RuntimePlayerKind.Client);
        var longImage = new string('a', 230);

        RuntimePlayerPropsApplier.ApplyConfirmed(
            player,
            [IncomingPlayerPropertyUpdate.String(PlayerPropertyId.BodyImage, longImage)]);

        Assert.Equal(new string('a', 223), player.BodyImage);
        Assert.False(player.MovementUpdated);
        Assert.False(player.TouchTestRequested);
    }
}

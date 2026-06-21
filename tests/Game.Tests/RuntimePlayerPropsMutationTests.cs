using Preagonal.GServer.Game;
using Preagonal.GServer.Protocol;
using Xunit;

namespace Preagonal.GServer.Game.Tests;

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
    public void MovementClearsPaused()
    {
        var player = new RuntimePlayer(7, "pc:Ruan", RuntimePlayerKind.Client);
        RuntimePlayerPropsApplier.ApplyConfirmed(
            player,
            [IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.Status, (byte)(PlayerStatus.Paused | PlayerStatus.Male))]);

        RuntimePlayerPropsApplier.ApplyConfirmed(
            player,
            [IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.X, 70)]);

        Assert.Equal(PlayerStatus.Male, player.Status);
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
            IncomingPlayerPropertyUpdate.NoValue(PlayerPropertyId.IpAddress),
            IncomingPlayerPropertyUpdate.NoValue(PlayerPropertyId.AccountName),
            IncomingPlayerPropertyUpdate.NoValue(PlayerPropertyId.Rating),
            IncomingPlayerPropertyUpdate.NoValue(PlayerPropertyId.JoinLeaveLevel),
            IncomingPlayerPropertyUpdate.NoValue(PlayerPropertyId.PlayerConnected),
            IncomingPlayerPropertyUpdate.NoValue(PlayerPropertyId.Unknown81),
            IncomingPlayerPropertyUpdate.NoValue(PlayerPropertyId.CommunityName)
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
            IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.Status, 33),
            IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.CarrySprite, 12),
            IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.HorseBushes, 6),
            IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.PlayerStatusMessage, 4),
            IncomingPlayerPropertyUpdate.GInt(PlayerPropertyId.UdpPort, 14900)
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
        Assert.Equal((PlayerStatus)33, player.Status);
        Assert.Equal(12, player.CarrySprite);
        Assert.Equal(6, player.HorseBombCount);
        Assert.Equal(4, player.StatusMessage);
        Assert.Equal(14900u, player.UdpPort);
        Assert.False(player.MovementUpdated);
        Assert.False(player.TouchTestRequested);
    }

    [Fact]
    public void AppliesConfirmedTerminalArrowEofValueWithCppClamp()
    {
        var player = new RuntimePlayer(7, "pc:Ruan", RuntimePlayerKind.Client);

        RuntimePlayerPropsApplier.ApplyConfirmed(
            player,
            [IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.ArrowsCount, 224)]);

        Assert.Equal(99, player.Arrows);
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

    [Fact]
    public void AppliesConfirmedHorseImagePropWithoutMovementSideEffects()
    {
        var player = new RuntimePlayer(7, "pc:Ruan", RuntimePlayerKind.Client);

        RuntimePlayerPropsApplier.ApplyConfirmed(
            player,
            [IncomingPlayerPropertyUpdate.String(PlayerPropertyId.HorseGif, "horse.png")]);

        Assert.Equal("horse.png", player.HorseImage);
        Assert.False(player.MovementUpdated);
        Assert.False(player.TouchTestRequested);
    }

    [Fact]
    public void AppliesConfirmedHorseImagePropAfterParserLimitAndOldSuffix()
    {
        var player = new RuntimePlayer(7, "pc:Ruan", RuntimePlayerKind.Client);

        RuntimePlayerPropsApplier.ApplyConfirmed(
            player,
            [IncomingPlayerPropertyUpdate.String(PlayerPropertyId.HorseGif, "horse.gif")]);

        Assert.Equal("horse.gif", player.HorseImage);
        Assert.False(player.MovementUpdated);
        Assert.False(player.TouchTestRequested);
    }

    [Fact]
    public void AppliesConfirmedCurrentChatPropWithoutMovementSideEffects()
    {
        var player = new RuntimePlayer(7, "pc:Ruan", RuntimePlayerKind.Client);

        RuntimePlayerPropsApplier.ApplyConfirmed(
            player,
            [IncomingPlayerPropertyUpdate.String(PlayerPropertyId.CurrentChat, "hello")]);

        Assert.Equal("hello", player.ChatMessage);
        Assert.False(player.MovementUpdated);
        Assert.False(player.TouchTestRequested);
    }

    [Fact]
    public void AppliesConfirmedAttachNpcIdIgnoringObjectTypeForState()
    {
        var player = new RuntimePlayer(7, "pc:Ruan", RuntimePlayerKind.Client);

        RuntimePlayerPropsApplier.ApplyConfirmed(
            player,
            [new IncomingPlayerPropertyUpdate(PlayerPropertyId.AttachNpc, GCharValue: 99, GIntValue: 123)]);

        Assert.Equal(123u, player.AttachedNpcId);
        Assert.False(player.MovementUpdated);
        Assert.False(player.TouchTestRequested);
    }

    [Fact]
    public void AppliesConfirmedAttachNpcUnsignedIdWhenSignedFallbackCannotRepresentIt()
    {
        var player = new RuntimePlayer(7, "pc:Ruan", RuntimePlayerKind.Client);

        RuntimePlayerPropsApplier.ApplyConfirmed(
            player,
            [new IncomingPlayerPropertyUpdate(PlayerPropertyId.AttachNpc, GCharValue: 224, GUIntValue: 4_294_438_880u)]);

        Assert.Equal(4_294_438_880u, player.AttachedNpcId);
        Assert.False(player.MovementUpdated);
        Assert.False(player.TouchTestRequested);
    }

    [Fact]
    public void AppliesConfirmedHeadImagePropWithCppLengthLimit()
    {
        var player = new RuntimePlayer(7, "pc:Ruan", RuntimePlayerKind.Client);
        var longImage = new string('h', 130);

        RuntimePlayerPropsApplier.ApplyConfirmed(
            player,
            [IncomingPlayerPropertyUpdate.String(PlayerPropertyId.HeadGif, longImage)]);

        Assert.Equal(new string('h', 123), player.HeadImage);
        Assert.False(player.MovementUpdated);
        Assert.False(player.TouchTestRequested);
    }

    [Fact]
    public void AppliesConfirmedSwordAndShieldDefaultImagesWithLimitsAndVersion()
    {
        var player = new RuntimePlayer(7, "pc:Ruan", RuntimePlayerKind.Client);
        var options = new RuntimePlayerPropsOptions(
            ClientVersion: ClientVersionId.Client1411,
            SwordLimit: 2,
            ShieldLimit: 2);

        RuntimePlayerPropsApplier.ApplyConfirmed(
            player,
            [
                IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.SwordPower, 4),
                IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.ShieldPower, 3)
            ],
            options);

        Assert.Equal(2, player.SwordPower);
        Assert.Equal("sword2.gif", player.SwordImage);
        Assert.Equal(2, player.ShieldPower);
        Assert.Equal("shield2.gif", player.ShieldImage);
    }

    [Fact]
    public void AppliesConfirmedSwordAndShieldCustomImagesWithCppTruncation()
    {
        var player = new RuntimePlayer(7, "pc:Ruan", RuntimePlayerKind.Client);
        var longSword = new string('s', 230);
        var longShield = new string('h', 230);

        RuntimePlayerPropsApplier.ApplyConfirmed(
            player,
            [
                new IncomingPlayerPropertyUpdate(PlayerPropertyId.SwordPower, GCharValue: 35, StringValue: longSword),
                new IncomingPlayerPropertyUpdate(PlayerPropertyId.ShieldPower, GCharValue: 12, StringValue: longShield)
            ]);

        Assert.Equal(3, player.SwordPower);
        Assert.Equal(new string('s', 223), player.SwordImage);
        Assert.Equal(2, player.ShieldPower);
        Assert.Equal(new string('h', 223), player.ShieldImage);
    }

    [Fact]
    public void IgnoresConfirmedShieldBugNoChangeUpdate()
    {
        var player = new RuntimePlayer(7, "pc:Ruan", RuntimePlayerKind.Client)
        {
            ShieldPower = 2,
            ShieldImage = "shield2.png"
        };

        RuntimePlayerPropsApplier.ApplyConfirmed(
            player,
            [IncomingPlayerPropertyUpdate.NoValue(PlayerPropertyId.ShieldPower)]);

        Assert.Equal(2, player.ShieldPower);
        Assert.Equal("shield2.png", player.ShieldImage);
    }

    [Fact]
    public void OldClientGaniBowPowerMutatesBowStateWithoutChangingModernGani()
    {
        var player = new RuntimePlayer(7, "pc:Ruan", RuntimePlayerKind.Client)
        {
            BowPower = 9,
            BowImage = "bow9.png"
        };
        var options = new RuntimePlayerPropsOptions(ClientVersion: ClientVersionId.Client1411);

        RuntimePlayerPropsApplier.ApplyConfirmed(
            player,
            [IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.Gani, 4)],
            options);

        Assert.Equal(4, player.BowPower);
        Assert.Equal(string.Empty, player.BowImage);
        Assert.Equal(string.Empty, player.Gani);
        Assert.False(player.MovementUpdated);
        Assert.False(player.TouchTestRequested);
    }

    [Fact]
    public void OldClientGaniBowImageMutatesBowImageWithoutChangingModernGani()
    {
        var player = new RuntimePlayer(7, "pc:Ruan", RuntimePlayerKind.Client);
        var options = new RuntimePlayerPropsOptions(ClientVersion: ClientVersionId.Client1411);

        RuntimePlayerPropsApplier.ApplyConfirmed(
            player,
            [new IncomingPlayerPropertyUpdate(PlayerPropertyId.Gani, GCharValue: 10, StringValue: "bow1.gif")],
            options);

        Assert.Equal(10, player.BowPower);
        Assert.Equal("bow1.gif", player.BowImage);
        Assert.Equal(string.Empty, player.Gani);
        Assert.False(player.MovementUpdated);
        Assert.False(player.TouchTestRequested);
    }

    [Fact]
    public void NicknameMutationStaysBlockedUnlessWordFilterAllowedBoundaryIsExplicit()
    {
        var player = new RuntimePlayer(7, "pc:7", RuntimePlayerKind.Client);

        var error = Assert.Throws<NotSupportedException>(() =>
            RuntimePlayerPropsApplier.ApplyConfirmed(
                player,
                [IncomingPlayerPropertyUpdate.String(PlayerPropertyId.Nickname, "Ruan")]));

        Assert.Contains("word filter", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, player.Nickname);
    }

    [Fact]
    public void AppliesConfirmedNicknameWithoutGuildAfterExplicitWordFilterAllow()
    {
        var player = new RuntimePlayer(7, "pc:7", RuntimePlayerKind.Client);
        var options = RuntimePlayerPropsOptions.Default with
        {
            NicknamePolicy = RuntimeNicknameUpdatePolicy.WordFilterAllowedNoGuild
        };

        RuntimePlayerPropsApplier.ApplyConfirmed(
            player,
            [IncomingPlayerPropertyUpdate.String(PlayerPropertyId.Nickname, "  **pc:7  ")],
            options);

        Assert.Equal("*pc:7", player.Nickname);
        Assert.Equal(string.Empty, player.Guild);
    }

    [Fact]
    public void NicknameWithGuildRemainsBlockedBecauseGuildValidationIsNotPorted()
    {
        var player = new RuntimePlayer(7, "pc:7", RuntimePlayerKind.Client);
        var options = RuntimePlayerPropsOptions.Default with
        {
            NicknamePolicy = RuntimeNicknameUpdatePolicy.WordFilterAllowedNoGuild
        };

        var error = Assert.Throws<NotSupportedException>(() =>
            RuntimePlayerPropsApplier.ApplyConfirmed(
                player,
                [IncomingPlayerPropertyUpdate.String(PlayerPropertyId.Nickname, "Ruan (LAT)")],
                options));

        Assert.Contains("guild", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, player.Nickname);
    }
}

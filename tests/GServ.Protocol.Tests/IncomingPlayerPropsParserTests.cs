using GServ.Protocol;
using Xunit;

namespace GServ.Protocol.Tests;

public sealed class IncomingPlayerPropsParserTests
{
    [Fact]
    public void ParsesConfirmedMovementPropsFromPlayerPropsBody()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.X);
        body.WriteGChar(70);
        body.WriteGChar((byte)PlayerPropertyId.Y);
        body.WriteGChar(71);
        body.WriteGChar((byte)PlayerPropertyId.Z);
        body.WriteGChar(55);
        body.WriteGChar((byte)PlayerPropertyId.Sprite);
        body.WriteGChar(2);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.True(result.Success);
        Assert.Equal(4, result.Updates.Count);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.X && update.GCharValue == 70);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.Y && update.GCharValue == 71);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.Z && update.GCharValue == 55);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.Sprite && update.GCharValue == 2);
    }

    [Fact]
    public void ParsesConfirmedStringAndPreciseCoordinateProps()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.CurrentLevel);
        body.WriteGChar(8);
        body.WriteBytes("start.nw"u8);
        body.WriteGChar((byte)PlayerPropertyId.Gani);
        body.WriteGChar(4);
        body.WriteBytes("walk"u8);
        body.WriteGChar((byte)PlayerPropertyId.X2);
        body.WriteGShort(1120);
        body.WriteGChar((byte)PlayerPropertyId.Y2);
        body.WriteGShort(1121);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.True(result.Success);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.CurrentLevel && update.StringValue == "start.nw");
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.Gani && update.StringValue == "walk");
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.X2 && update.GShortValue == 1120);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.Y2 && update.GShortValue == 1121);
    }

    [Fact]
    public void StopsOnFirstInvalidUnknown77PropertyLikeCppSetPropsDefaultReturn()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.X);
        body.WriteGChar(70);
        body.WriteGChar(77);
        body.WriteGChar((byte)PlayerPropertyId.Y);
        body.WriteGChar(71);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.False(result.Success);
        Assert.Equal((PlayerPropertyId)77, result.UnsupportedPropertyId);
        Assert.Single(result.Updates);
        Assert.DoesNotContain(result.Updates, update => update.PropertyId == PlayerPropertyId.Y);
    }

    [Fact]
    public void ParsesConfirmedReadOnlyAndNoBytePropsWithoutInventingValues()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.Id);
        body.WriteGShort(7);
        body.WriteGChar((byte)PlayerPropertyId.KillsCount);
        body.WriteGInt(111);
        body.WriteGChar((byte)PlayerPropertyId.DeathsCount);
        body.WriteGInt(222);
        body.WriteGChar((byte)PlayerPropertyId.OnlineSeconds);
        body.WriteGInt(333);
        body.WriteGChar((byte)PlayerPropertyId.JoinLeaveLevel);
        body.WriteGChar((byte)PlayerPropertyId.PlayerConnected);
        body.WriteGChar((byte)PlayerPropertyId.Unknown81);
        body.WriteGChar(3);
        body.WriteGChar((byte)PlayerPropertyId.X);
        body.WriteGChar(70);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.True(result.Success);
        Assert.Equal(
            [
                PlayerPropertyId.Id,
                PlayerPropertyId.KillsCount,
                PlayerPropertyId.DeathsCount,
                PlayerPropertyId.OnlineSeconds,
                PlayerPropertyId.JoinLeaveLevel,
                PlayerPropertyId.PlayerConnected,
                PlayerPropertyId.Unknown81,
                PlayerPropertyId.X
            ],
            result.Updates.Select(update => update.PropertyId));
        Assert.All(result.Updates.Take(7), update =>
        {
            Assert.Null(update.GCharValue);
            Assert.Null(update.GShortValue);
            Assert.Null(update.StringValue);
        });
    }

    [Fact]
    public void ParsesConfirmedScalarInventoryAndStatProps()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.MaxPower);
        body.WriteGChar(15);
        body.WriteGChar((byte)PlayerPropertyId.CurrentPower);
        body.WriteGChar(11);
        body.WriteGChar((byte)PlayerPropertyId.RupeesCount);
        body.WriteGInt(3_000_000);
        body.WriteGChar((byte)PlayerPropertyId.ArrowsCount);
        body.WriteGChar(150);
        body.WriteGChar((byte)PlayerPropertyId.BombsCount);
        body.WriteGChar(151);
        body.WriteGChar((byte)PlayerPropertyId.GlovePower);
        body.WriteGChar(9);
        body.WriteGChar((byte)PlayerPropertyId.BombPower);
        body.WriteGChar(8);
        body.WriteGChar((byte)PlayerPropertyId.ApCounter);
        body.WriteGShort(123);
        body.WriteGChar((byte)PlayerPropertyId.MagicPoints);
        body.WriteGChar(200);
        body.WriteGChar((byte)PlayerPropertyId.AdditionalFlags);
        body.WriteGChar(77);
        body.WriteGChar((byte)PlayerPropertyId.Alignment);
        body.WriteGChar(120);
        body.WriteGChar((byte)PlayerPropertyId.CarrySprite);
        body.WriteGChar(12);
        body.WriteGChar((byte)PlayerPropertyId.HorseBushes);
        body.WriteGChar(6);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.True(result.Success);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.MaxPower && update.GCharValue == 15);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.CurrentPower && update.GCharValue == 11);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.RupeesCount && update.GIntValue == 3_000_000);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.ArrowsCount && update.GCharValue == 150);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.BombsCount && update.GCharValue == 151);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.GlovePower && update.GCharValue == 9);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.BombPower && update.GCharValue == 8);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.ApCounter && update.GShortValue == 123);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.MagicPoints && update.GCharValue == 200);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.AdditionalFlags && update.GCharValue == 77);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.Alignment && update.GCharValue == 120);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.CarrySprite && update.GCharValue == 12);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.HorseBushes && update.GCharValue == 6);
    }

    [Fact]
    public void ParsesConfirmedEnvironmentAndGaniAttributeProps()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.PlayerLanguage);
        body.WriteGChar(2);
        body.WriteBytes("pt"u8);
        body.WriteGChar((byte)PlayerPropertyId.OsType);
        body.WriteGChar(4);
        body.WriteBytes("wind"u8);
        body.WriteGChar((byte)PlayerPropertyId.TextCodePage);
        body.WriteGInt(1252);
        body.WriteGChar((byte)PlayerPropertyId.GAttrib1);
        body.WriteGChar(5);
        body.WriteBytes("sword"u8);
        body.WriteGChar((byte)PlayerPropertyId.GAttrib30);
        body.WriteGChar(4);
        body.WriteBytes("tail"u8);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.True(result.Success);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.PlayerLanguage && update.StringValue == "pt");
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.OsType && update.StringValue == "wind");
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.TextCodePage && update.GIntValue == 1252);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.GAttrib1 && update.StringValue == "sword");
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.GAttrib30 && update.StringValue == "tail");
    }

    [Fact]
    public void ParsesConfirmedBodyImageProp()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.BodyImage);
        body.WriteGChar(8);
        body.WriteBytes("body.png"u8);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.True(result.Success);
        var update = Assert.Single(result.Updates);
        Assert.Equal(PlayerPropertyId.BodyImage, update.PropertyId);
        Assert.Equal("body.png", update.StringValue);
    }

    [Fact]
    public void ParsesConfirmedColorPropAsFiveGChars()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.Colors);
        body.WriteGChar(1);
        body.WriteGChar(2);
        body.WriteGChar(3);
        body.WriteGChar(4);
        body.WriteGChar(5);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.True(result.Success);
        var update = Assert.Single(result.Updates);
        Assert.Equal(PlayerPropertyId.Colors, update.PropertyId);
        Assert.Equal([1, 2, 3, 4, 5], update.BytesValue);
    }

    [Fact]
    public void ParsesConfirmedEffectColorsByConsumingOptionalGInt4()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.EffectColors);
        body.WriteGChar(0);
        body.WriteGChar((byte)PlayerPropertyId.EffectColors);
        body.WriteGChar(1);
        body.WriteGInt4(0x01020304);
        body.WriteGChar((byte)PlayerPropertyId.X);
        body.WriteGChar(70);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.True(result.Success);
        Assert.Equal(
            [
                PlayerPropertyId.EffectColors,
                PlayerPropertyId.EffectColors,
                PlayerPropertyId.X
            ],
            result.Updates.Select(update => update.PropertyId));
        Assert.All(result.Updates.Take(2), update =>
        {
            Assert.Null(update.GCharValue);
            Assert.Null(update.GIntValue);
            Assert.Null(update.BytesValue);
        });
        Assert.Equal((byte)70, result.Updates[2].GCharValue);
    }

    [Fact]
    public void ParsesConfirmedRatingByConsumingGIntWithoutInventingMutationValue()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.Rating);
        body.WriteGInt(123456);
        body.WriteGChar((byte)PlayerPropertyId.X);
        body.WriteGChar(70);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.True(result.Success);
        Assert.Equal([PlayerPropertyId.Rating, PlayerPropertyId.X], result.Updates.Select(update => update.PropertyId));
        Assert.Null(result.Updates[0].GIntValue);
        Assert.Null(result.Updates[0].GCharValue);
        Assert.Equal((byte)70, result.Updates[1].GCharValue);
    }

    [Fact]
    public void ParsesConfirmedAccountNameByConsumingStringWithoutInventingMutationValue()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.AccountName);
        body.WriteGChar(4);
        body.WriteBytes("Ruan"u8);
        body.WriteGChar((byte)PlayerPropertyId.X);
        body.WriteGChar(70);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.True(result.Success);
        Assert.Equal([PlayerPropertyId.AccountName, PlayerPropertyId.X], result.Updates.Select(update => update.PropertyId));
        Assert.Null(result.Updates[0].StringValue);
        Assert.Null(result.Updates[0].GCharValue);
        Assert.Equal((byte)70, result.Updates[1].GCharValue);
    }

    [Fact]
    public void ParsesConfirmedCommunityNameByConsumingStringWithoutInventingMutationValue()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.CommunityName);
        body.WriteGChar(8);
        body.WriteBytes("commname"u8);
        body.WriteGChar((byte)PlayerPropertyId.X);
        body.WriteGChar(70);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.True(result.Success);
        Assert.Equal([PlayerPropertyId.CommunityName, PlayerPropertyId.X], result.Updates.Select(update => update.PropertyId));
        Assert.Null(result.Updates[0].StringValue);
        Assert.Null(result.Updates[0].GCharValue);
        Assert.Equal((byte)70, result.Updates[1].GCharValue);
    }

    [Fact]
    public void BuildsConfirmedForwardedMovementPropsForPreciseSender()
    {
        var updates = new[]
        {
            IncomingPlayerPropertyUpdate.GShort(PlayerPropertyId.X2, 1120),
            IncomingPlayerPropertyUpdate.GShort(PlayerPropertyId.Y2, 1120)
        };

        var packet = IncomingPlayerPropsForwarding.BuildOtherPlayerPropsPacket(
            playerId: 7,
            pixelX: 560,
            pixelY: 560,
            pixelZ: 0,
            updates,
            senderSupportsPreciseMovement: true,
            appendNewline: true);

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
            packet);
    }

    [Fact]
    public void DoesNotForwardConfirmedReadOnlyNoLocalProps()
    {
        var updates = new[]
        {
            IncomingPlayerPropertyUpdate.NoValue(PlayerPropertyId.KillsCount),
            IncomingPlayerPropertyUpdate.NoValue(PlayerPropertyId.DeathsCount),
            IncomingPlayerPropertyUpdate.NoValue(PlayerPropertyId.OnlineSeconds),
            IncomingPlayerPropertyUpdate.NoValue(PlayerPropertyId.Unknown81)
        };

        var packet = IncomingPlayerPropsForwarding.BuildOtherPlayerPropsPacket(
            playerId: 7,
            pixelX: 0,
            pixelY: 0,
            pixelZ: 0,
            updates,
            senderSupportsPreciseMovement: true,
            appendNewline: true);

        Assert.Equal([40, 32, 39, 10], packet);
    }

    [Fact]
    public void DoesNotForwardConfirmedEffectColorsBecauseSendLocalIsFalse()
    {
        var packet = IncomingPlayerPropsForwarding.BuildOtherPlayerPropsPacket(
            playerId: 7,
            pixelX: 0,
            pixelY: 0,
            pixelZ: 0,
            [IncomingPlayerPropertyUpdate.NoValue(PlayerPropertyId.EffectColors)],
            senderSupportsPreciseMovement: true,
            appendNewline: true);

        Assert.Equal([40, 32, 39, 10], packet);
    }

    [Fact]
    public void ForwardsConfirmedApCounterUsingCppGetPropPlusOneBehavior()
    {
        var packet = IncomingPlayerPropsForwarding.BuildOtherPlayerPropsPacket(
            playerId: 7,
            pixelX: 0,
            pixelY: 0,
            pixelZ: 0,
            [IncomingPlayerPropertyUpdate.GShort(PlayerPropertyId.ApCounter, 123)],
            senderSupportsPreciseMovement: false,
            appendNewline: true);

        Assert.Equal([40, 32, 39, 57, 32, 156, 10], packet);
    }

    [Fact]
    public void ForwardsConfirmedGaniAttributesWithOriginalStringPayload()
    {
        var packet = IncomingPlayerPropsForwarding.BuildOtherPlayerPropsPacket(
            playerId: 7,
            pixelX: 0,
            pixelY: 0,
            pixelZ: 0,
            [IncomingPlayerPropertyUpdate.String(PlayerPropertyId.GAttrib1, "sword")],
            senderSupportsPreciseMovement: true,
            appendNewline: true);

        Assert.Equal([40, 32, 39, 69, 37, 115, 119, 111, 114, 100, 10], packet);
    }

    [Fact]
    public void ForwardsConfirmedColorsWithFiveGChars()
    {
        var packet = IncomingPlayerPropsForwarding.BuildOtherPlayerPropsPacket(
            playerId: 7,
            pixelX: 0,
            pixelY: 0,
            pixelZ: 0,
            [IncomingPlayerPropertyUpdate.Bytes(PlayerPropertyId.Colors, [1, 2, 3, 4, 5])],
            senderSupportsPreciseMovement: true,
            appendNewline: true);

        Assert.Equal([40, 32, 39, 45, 33, 34, 35, 36, 37, 10], packet);
    }

    [Fact]
    public void ForwardsConfirmedBodyImageWithCurrentStringPayload()
    {
        var packet = IncomingPlayerPropsForwarding.BuildOtherPlayerPropsPacket(
            playerId: 7,
            pixelX: 0,
            pixelY: 0,
            pixelZ: 0,
            [IncomingPlayerPropertyUpdate.String(PlayerPropertyId.BodyImage, "body.png")],
            senderSupportsPreciseMovement: true,
            appendNewline: true);

        Assert.Equal([40, 32, 39, 67, 40, 98, 111, 100, 121, 46, 112, 110, 103, 10], packet);
    }
}

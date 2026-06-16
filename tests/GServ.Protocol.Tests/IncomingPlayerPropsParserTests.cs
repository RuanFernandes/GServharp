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
}

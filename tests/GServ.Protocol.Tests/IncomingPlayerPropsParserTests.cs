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
    public void StopsOnFirstUnconfirmedPropertyLikeCppSetPropsDefaultReturn()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.X);
        body.WriteGChar(70);
        body.WriteGChar((byte)PlayerPropertyId.RupeesCount);
        body.WriteGInt(1234);
        body.WriteGChar((byte)PlayerPropertyId.Y);
        body.WriteGChar(71);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.False(result.Success);
        Assert.Equal(PlayerPropertyId.RupeesCount, result.UnsupportedPropertyId);
        Assert.Single(result.Updates);
        Assert.DoesNotContain(result.Updates, update => update.PropertyId == PlayerPropertyId.Y);
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
}

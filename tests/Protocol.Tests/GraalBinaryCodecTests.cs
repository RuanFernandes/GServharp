using GServ.Protocol;
using Xunit;

namespace GServ.Protocol.Tests;

public sealed class GraalBinaryCodecTests
{
    [Theory]
    [InlineData(0, 32)]
    [InlineData(25, 57)]
    [InlineData(222, 254)]
    [InlineData(223, 255)]
    [InlineData(255, 255)]
    public void WriteGCharMatchesCStringClampBehavior(byte value, byte expected)
    {
        var writer = new GraalBinaryWriter();

        writer.WriteGChar(value);

        Assert.Equal(new[] { expected }, writer.ToArray());
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(28767u)]
    [InlineData(3682399u)]
    [InlineData(471347295u)]
    [InlineData(uint.MaxValue)]
    public void GraalIntegersRoundTripConfirmedRanges(uint value)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGShort((ushort)Math.Min(value, 28767u));
        writer.WriteGInt(Math.Min(value, 3682399u));
        writer.WriteGInt4(Math.Min(value, 471347295u));
        writer.WriteGInt5(value);

        var reader = new GraalBinaryReader(writer.ToArray());

        Assert.Equal((ushort)Math.Min(value, 28767u), reader.ReadGShort());
        Assert.Equal((int)Math.Min(value, 3682399u), reader.ReadGInt());
        Assert.Equal((int)Math.Min(value, 471347295u), reader.ReadGInt4());
        Assert.Equal(value, reader.ReadGInt5());
    }

    [Fact]
    public void RawShortAndIntUseBigEndianCStringOrder()
    {
        var writer = new GraalBinaryWriter();

        writer.WriteRawShort(0x1234);
        writer.WriteRawInt(0x01020304);

        Assert.Equal(new byte[] { 0x12, 0x34, 0x01, 0x02, 0x03, 0x04 }, writer.ToArray());
    }

    [Fact]
    public void ReadGCharPastEndMatchesCStringZeroFilledReadBehavior()
    {
        var reader = new GraalBinaryReader([]);

        Assert.Equal(224, reader.ReadGChar());
    }
}

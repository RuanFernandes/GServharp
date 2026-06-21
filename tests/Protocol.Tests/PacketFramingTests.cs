using System.Text;
using Preagonal.GServer.Protocol;
using Xunit;

namespace Preagonal.GServer.Protocol.Tests;

public sealed class PacketFramingTests
{
    [Fact]
    public void NormalPacketsSplitOnNewlineWithoutIncludingDelimiter()
    {
        var packets = PacketFramer.SplitNewlinePackets(Encoding.ASCII.GetBytes("one\ntwo\n"));

        Assert.Collection(
            packets,
            p => Assert.Equal("one", Encoding.ASCII.GetString(p.Payload.Span)),
            p => Assert.Equal("two", Encoding.ASCII.GetString(p.Payload.Span)));
    }

    [Fact]
    public void RawDataPacketMakesNextPacketLengthBased()
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)PlayerToServerPacketId.RawData);
        writer.WriteGInt(3);
        writer.WriteByte((byte)'\n');
        writer.WriteBytes(Encoding.ASCII.GetBytes("abc"));

        var packets = PacketFramer.ParseClientPackets(writer.ToArray());

        Assert.Collection(
            packets,
            p => Assert.Equal(PlayerToServerPacketId.RawData, p.Id),
            p =>
            {
                Assert.Null(p.Id);
                Assert.Equal("abc", Encoding.ASCII.GetString(p.Payload.Span));
            });
    }

    [Fact]
    public void LengthPrefixedFramesUseRawBigEndianShortBeforeInnerPayload()
    {
        var bytes = new byte[] { 0, 3, (byte)'a', (byte)'b', (byte)'c', 0, 2, (byte)'d', (byte)'e' };

        var frames = PacketFramer.ReadLengthPrefixedFrames(bytes);

        Assert.Collection(
            frames,
            f => Assert.Equal("abc", Encoding.ASCII.GetString(f.Span)),
            f => Assert.Equal("de", Encoding.ASCII.GetString(f.Span)));
    }

    [Fact]
    public void IncompleteLengthPrefixedFrameIsNotReturned()
    {
        var bytes = new byte[] { 0, 4, (byte)'a', (byte)'b' };

        var frames = PacketFramer.ReadLengthPrefixedFrames(bytes);

        Assert.Empty(frames);
    }

    [Fact]
    public void RawDataPayloadCanStripClientTrailingNewline()
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)PlayerToServerPacketId.RawData);
        writer.WriteGInt(4);
        writer.WriteByte((byte)'\n');
        writer.WriteBytes(Encoding.ASCII.GetBytes("abc\n"));

        var packets = PacketFramer.ParseClientPackets(
            writer.ToArray(),
            new ClientPacketParseOptions(StripRawDataTrailingNewline: true));

        Assert.Collection(
            packets,
            p => Assert.Equal(PlayerToServerPacketId.RawData, p.Id),
            p => Assert.Equal("abc", Encoding.ASCII.GetString(p.Payload.Span)));
    }

    [Fact]
    public void StatefulClientFramerCarriesRawDataLengthToNextDecodedPayload()
    {
        var header = new GraalBinaryWriter();
        header.WriteGChar((byte)PlayerToServerPacketId.RawData);
        header.WriteGInt(4);
        header.WriteByte((byte)'\n');
        var framer = new ClientPacketStreamFramer(new ClientPacketParseOptions(StripRawDataTrailingNewline: true));

        var first = framer.Parse(header.ToArray());
        var second = framer.Parse("abc\n"u8);

        var firstPacket = Assert.Single(first);
        Assert.Equal(PlayerToServerPacketId.RawData, firstPacket.Id);
        var rawPacket = Assert.Single(second);
        Assert.Null(rawPacket.Id);
        Assert.Equal("abc", Encoding.ASCII.GetString(rawPacket.Payload.Span));
    }

    [Fact]
    public void BundleUsesRawBigEndianShortLengthPrefixes()
    {
        var payload = new byte[] { 0, 3, 1, 2, 3, 0, 2, 4, 5 };

        var packets = PacketFramer.ReadBundle(payload);

        Assert.Collection(
            packets,
            p => Assert.Equal(new byte[] { 1, 2, 3 }, p.ToArray()),
            p => Assert.Equal(new byte[] { 4, 5 }, p.ToArray()));
    }

    [Fact]
    public void ClientBundleExpandsInnerPackets()
    {
        var first = new GraalBinaryWriter();
        first.WriteGChar((byte)PlayerToServerPacketId.ItemAdd);
        first.WriteGChar(20);
        first.WriteGChar(22);
        first.WriteGChar(3);
        var second = new GraalBinaryWriter();
        second.WriteGChar((byte)PlayerToServerPacketId.OpenChest);
        second.WriteGChar(20);
        second.WriteGChar(24);
        var bundle = new GraalBinaryWriter();
        bundle.WriteByte(WrappedGChar((byte)PlayerToServerPacketId.Bundle));
        bundle.WriteRawShort((ushort)first.ToArray().Length);
        bundle.WriteBytes(first.ToArray());
        bundle.WriteRawShort((ushort)second.ToArray().Length);
        bundle.WriteBytes(second.ToArray());
        bundle.WriteByte((byte)'\n');

        var packets = PacketFramer.ParseClientPackets(bundle.ToArray());

        Assert.Collection(
            packets,
            p => Assert.Equal(PlayerToServerPacketId.ItemAdd, p.Id),
            p => Assert.Equal(PlayerToServerPacketId.OpenChest, p.Id));
    }

    private static byte WrappedGChar(byte value) => unchecked((byte)(value + 32));
}

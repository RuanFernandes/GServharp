using System.Text;
using GServ.Protocol;
using Xunit;

namespace GServ.Protocol.Tests;

public sealed class FileQueueCompatibilityTests
{
    [Fact]
    public void Gen1FlushSendsNormalNewlinePacketsUncompressedInQueueOrder()
    {
        var queue = new GraalFileQueue();

        queue.AddPacket(Encoding.ASCII.GetBytes("abc\nxyz\n"));

        Assert.Equal(
            Encoding.ASCII.GetBytes("abc\nxyz\n"),
            queue.FlushUncompressed());
    }

    [Fact]
    public void RawDataHeaderAndBoardPayloadStayInNormalQueue()
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.RawData);
        writer.WriteGInt(2);
        writer.WriteByte((byte)'\n');
        writer.WriteGChar((byte)ServerToPlayerPacketId.BoardPacket);
        writer.WriteByte((byte)'x');

        var queue = new GraalFileQueue();
        queue.AddPacket(writer.ToArray());

        Assert.Equal(writer.ToArray(), queue.FlushUncompressed());
    }

    [Fact]
    public void PartialSocketSendLeavesRemainingOutputBufferedForNextFlush()
    {
        var queue = new GraalFileQueue();
        queue.AddPacket(Encoding.ASCII.GetBytes("abcdef\n"));

        Assert.Equal(Encoding.ASCII.GetBytes("abc"), queue.FlushUncompressed(maxBytes: 3));
        Assert.Equal(Encoding.ASCII.GetBytes("def\n"), queue.FlushUncompressed());
    }

    [Fact]
    public void Gen5ShortPayloadFlushPrefixesLengthCompressionTypeAndEncryptedPayload()
    {
        var queue = new GraalFileQueue();
        queue.SetCodec(EncryptionGeneration.Gen5, key: 0);
        queue.AddPacket(Encoding.ASCII.GetBytes("abc\n"));

        Assert.Equal(
            new byte[] { 0x00, 0x05, 0x02, 0x79, 0x7A, 0xB2, 0xDC },
            queue.FlushSocket());
    }

    [Fact]
    public void Gen5ShortPayloadPartialSocketSendLeavesRemainingFramedBytesBuffered()
    {
        var queue = new GraalFileQueue();
        queue.SetCodec(EncryptionGeneration.Gen5, key: 0);
        queue.AddPacket(Encoding.ASCII.GetBytes("abc\n"));

        Assert.Equal(new byte[] { 0x00, 0x05, 0x02 }, queue.FlushSocket(maxBytes: 3));
        Assert.Equal(new byte[] { 0x79, 0x7A, 0xB2, 0xDC }, queue.FlushSocket());
    }

    [Fact]
    public void WebSocketSocketFlushWrapsFramedPayloadAfterCompression()
    {
        var queue = new GraalFileQueue();
        queue.SetCodec(EncryptionGeneration.Gen5, key: 0);
        queue.AddPacket(Encoding.ASCII.GetBytes("abc\n"));

        Assert.Equal(
            new byte[] { 0x82, 0x07, 0x00, 0x05, 0x02, 0x79, 0x7A, 0xB2, 0xDC },
            queue.FlushSocket(wrapWebSocket: true));
    }

    [Fact]
    public void Gen1SocketFlushSendsQueuedBytesWithoutOuterLengthPrefix()
    {
        var queue = new GraalFileQueue();
        queue.SetCodec(EncryptionGeneration.Gen1, key: 0);
        queue.AddPacket(Encoding.ASCII.GetBytes("abc\n"));

        Assert.Equal(Encoding.ASCII.GetBytes("abc\n"), queue.FlushSocket());
    }

    [Fact]
    public void Gen5ZlibPayloadFlushMatchesGs2libFixture()
    {
        var queue = new GraalFileQueue();
        queue.SetCodec(EncryptionGeneration.Gen5, key: 0);
        queue.AddPacket(Encoding.ASCII.GetBytes(new string('a', 55) + "\n"));

        Assert.Equal(
            new byte[] { 0x00, 0x0E, 0x04, 0x60, 0x84, 0x9A, 0x9A, 0x5C, 0xD3, 0x31, 0x82, 0x58, 0x46, 0x1C, 0x13, 0x5A },
            queue.FlushSocket());
    }

    [Fact]
    public void Gen5PayloadAtUncompressedThresholdMatchesGs2libFixture()
    {
        var queue = new GraalFileQueue();
        queue.SetCodec(EncryptionGeneration.Gen5, key: 0);
        queue.AddPacket(Encoding.ASCII.GetBytes(new string('a', 54) + "\n"));

        Assert.Equal(
            new byte[]
            {
                0x00, 0x38, 0x02, 0x79, 0x79, 0xB0, 0xB7, 0x19,
                0xB9, 0x20, 0xE2, 0x39, 0x7B, 0xC6, 0x66, 0xD9,
                0x82, 0xF9, 0x83, 0xF9, 0x33, 0x46, 0x41, 0x99,
                0x9D, 0x7B, 0x5D, 0xB9, 0xB1, 0xD7, 0xDF, 0x59,
                0x15, 0x60, 0x25, 0x79, 0x44, 0xD5, 0x14, 0x19,
                0x78, 0x04, 0x79, 0x39, 0x3E, 0xBA, 0x47, 0xD9,
                0x5D, 0x53, 0xFB, 0x61, 0x61, 0x61, 0x61, 0x61,
                0x61, 0x0A,
            },
            queue.FlushSocket());
    }

    [Fact]
    public void Gen5Bzip2PayloadFlushMatchesGs2libFixture()
    {
        var queue = new GraalFileQueue();
        queue.SetCodec(EncryptionGeneration.Gen5, key: 0);
        var payload = Encoding.ASCII.GetBytes(new string('a', 8192) + "\n");
        queue.AddPacket(payload);

        Assert.Equal(
            new byte[]
            {
                0x00, 0x32, 0x06, 0x5A, 0x42, 0xB9, 0xE7, 0x49,
                0x99, 0x18, 0xA5, 0x0B, 0x43, 0xD4, 0x4B, 0x64,
                0x99, 0x98, 0xE2, 0x12, 0xE1, 0x00, 0x80, 0x10,
                0x00, 0x04, 0x20, 0x00, 0x00, 0x08, 0x20, 0x00,
                0x30, 0xCD, 0x34, 0x0A, 0xA3, 0x1F, 0x0A, 0x0B,
                0x00, 0x61, 0x77, 0x24, 0x53, 0x85, 0x09, 0x07,
                0x34, 0xCD, 0xC7, 0xA0
            },
            queue.FlushSocket());
    }

    [Fact]
    public void Gen2ShortPayloadFlushMatchesGs2libZlibFixture()
    {
        var queue = new GraalFileQueue();
        queue.SetCodec(EncryptionGeneration.Gen2, key: 0);
        queue.AddPacket(Encoding.ASCII.GetBytes("abc\n"));

        Assert.Equal(
            new byte[] { 0x00, 0x0C, 0x78, 0x9C, 0x4B, 0x4C, 0x4A, 0xE6, 0x02, 0x00, 0x03, 0x7E, 0x01, 0x31 },
            queue.FlushSocket());
    }

    [Fact]
    public void Gen3ShortPayloadFlushMatchesGs2libZlibFixtureWithoutGen3Insertion()
    {
        var queue = new GraalFileQueue();
        queue.SetCodec(EncryptionGeneration.Gen3, key: 0);
        queue.AddPacket(Encoding.ASCII.GetBytes("abc\n"));

        Assert.Equal(
            new byte[] { 0x00, 0x0C, 0x78, 0x9C, 0x4B, 0x4C, 0x4A, 0xE6, 0x02, 0x00, 0x03, 0x7E, 0x01, 0x31 },
            queue.FlushSocket());
    }

    [Fact]
    public void Gen4ShortPayloadFlushMatchesGs2libBzip2Fixture()
    {
        var queue = new GraalFileQueue();
        queue.SetCodec(EncryptionGeneration.Gen4, key: 0);
        queue.AddPacket(Encoding.ASCII.GetBytes("abc\n"));

        Assert.Equal(
            new byte[]
            {
                0x00, 0x2A, 0x5A, 0x42, 0xB9, 0xE7, 0x49, 0x99,
                0x18, 0xA5, 0x0B, 0x43, 0x0A, 0x60, 0xED, 0x35,
                0x98, 0xE2, 0x00, 0xC1, 0x00, 0x00, 0x10, 0x38,
                0x00, 0x20, 0x00, 0x21, 0x9A, 0x68, 0x33, 0x4D,
                0x13, 0x3C, 0x5D, 0xC9, 0x14, 0xE1, 0x42, 0x42,
                0xB5, 0x9D, 0x57, 0x58
            },
            queue.FlushSocket());
    }

    [Fact]
    public void Gen2LongPayloadFlushMatchesGs2libZlibFixture()
    {
        var queue = new GraalFileQueue();
        queue.SetCodec(EncryptionGeneration.Gen2, key: 0);
        queue.AddPacket(Encoding.ASCII.GetBytes(new string('a', 100) + "\n"));

        Assert.Equal(
            new byte[] { 0x00, 0x0D, 0x78, 0x9C, 0x4B, 0x4C, 0xA4, 0x3D, 0xE0, 0x02, 0x00, 0xA0, 0x36, 0x25, 0xEF },
            queue.FlushSocket());
    }
}

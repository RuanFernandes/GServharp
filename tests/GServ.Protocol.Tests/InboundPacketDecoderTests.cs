using System.Text;
using GServ.Protocol;
using Xunit;

namespace GServ.Protocol.Tests;

public sealed class InboundPacketDecoderTests
{
    [Fact]
    public void Gen5UncompressedFramePayloadDecodesConfirmedFixture()
    {
        var decoder = new InboundPacketDecoder(EncryptionGeneration.Gen5, key: 0);

        var result = decoder.DecodeSocketFramePayload([0x02, 0x79, 0x7A, 0xB2, 0xDC]);

        var packet = Assert.Single(result.Packets);
        Assert.Equal("abc", Encoding.ASCII.GetString(packet));
    }

    [Fact]
    public void Gen5ZlibFramePayloadDecodesConfirmedFixture()
    {
        var decoder = new InboundPacketDecoder(EncryptionGeneration.Gen5, key: 0);

        var result = decoder.DecodeSocketFramePayload([
            0x04, 0x60, 0x84, 0x9A, 0x9A, 0x5C, 0xD3, 0x31,
            0x82, 0x58, 0x46, 0x1C, 0x13, 0x5A
        ]);

        var packet = Assert.Single(result.Packets);
        Assert.Equal(new string('a', 55), Encoding.ASCII.GetString(packet));
    }

    [Fact]
    public void Gen2ZlibFramePayloadDecodesConfirmedFixture()
    {
        var decoder = new InboundPacketDecoder(EncryptionGeneration.Gen2, key: 0);

        var result = decoder.DecodeSocketFramePayload([
            0x78, 0x9C, 0x4B, 0x4C, 0x4A, 0xE6, 0x02, 0x00, 0x03, 0x7E, 0x01, 0x31
        ]);

        var packet = Assert.Single(result.Packets);
        Assert.Equal("abc", Encoding.ASCII.GetString(packet));
    }

    [Fact]
    public void DecodeSocketFrameExposesRawDecodedPayloadBeforePacketFraming()
    {
        var decoder = new InboundPacketDecoder(EncryptionGeneration.Gen5, key: 0);

        var result = decoder.DecodeSocketFrame([0x02, 0x79, 0x7A, 0xB2, 0xDC]);

        Assert.Equal("abc\n", Encoding.ASCII.GetString(result.DecodedPayload));
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Gen5InvalidCompressionTypeDecryptsAndContinuesWithWarningLikeCpp()
    {
        var codec = new GraalEncryption(EncryptionGeneration.Gen5);
        codec.Reset(0);
        var encrypted = codec.Encrypt("abc\n"u8);
        var framePayload = new byte[encrypted.Length + 1];
        framePayload[0] = 0x08;
        encrypted.CopyTo(framePayload.AsSpan(1));
        var decoder = new InboundPacketDecoder(EncryptionGeneration.Gen5, key: 0);

        var result = decoder.DecodeSocketFramePayload(framePayload);

        var packet = Assert.Single(result.Packets);
        Assert.Equal("abc", Encoding.ASCII.GetString(packet));
        Assert.Contains(result.Warnings, warning => warning.Contains("incorrect packet compression type 0x08", StringComparison.Ordinal));
    }

    [Fact]
    public void Gen4Bzip2FramePayloadDecodesConfirmedFixture()
    {
        var decoder = new InboundPacketDecoder(EncryptionGeneration.Gen4, key: 0);

        var result = decoder.DecodeSocketFramePayload([
            0x5A, 0x42, 0xB9, 0xE7, 0x49, 0x99, 0x18, 0xA5,
            0x0B, 0x43, 0x0A, 0x60, 0xED, 0x35, 0x98, 0xE2,
            0x00, 0xC1, 0x00, 0x00, 0x10, 0x38, 0x00, 0x20,
            0x00, 0x21, 0x9A, 0x68, 0x33, 0x4D, 0x13, 0x3C,
            0x5D, 0xC9, 0x14, 0xE1, 0x42, 0x42, 0xB5, 0x9D,
            0x57, 0x58
        ]);

        var packet = Assert.Single(result.Packets);
        Assert.Equal("abc", Encoding.ASCII.GetString(packet));
    }

    [Fact]
    public void Gen5Bzip2FramePayloadDecodesConfirmedFixture()
    {
        var decoder = new InboundPacketDecoder(EncryptionGeneration.Gen5, key: 0);

        var result = decoder.DecodeSocketFramePayload([
            0x06, 0x5A, 0x42, 0xB9, 0xE7, 0x49, 0x99, 0x18,
            0xA5, 0x0B, 0x43, 0xD4, 0x4B, 0x64, 0x99, 0x98,
            0xE2, 0x12, 0xE1, 0x00, 0x80, 0x10, 0x00, 0x04,
            0x20, 0x00, 0x00, 0x08, 0x20, 0x00, 0x30, 0xCD,
            0x34, 0x0A, 0xA3, 0x1F, 0x0A, 0x0B, 0x00, 0x61,
            0x77, 0x24, 0x53, 0x85, 0x09, 0x07, 0x34, 0xCD,
            0xC7, 0xA0
        ]);

        var packet = Assert.Single(result.Packets);
        Assert.Equal(new string('a', 8192), Encoding.ASCII.GetString(packet));
    }
}

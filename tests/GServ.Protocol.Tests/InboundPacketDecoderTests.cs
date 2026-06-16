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
    public void Gen4InboundBzip2BranchRemainsExplicitlyBlocked()
    {
        var decoder = new InboundPacketDecoder(EncryptionGeneration.Gen4, key: 0);

        var ex = Assert.Throws<NotSupportedException>(() => decoder.DecodeSocketFramePayload([1, 2, 3]));

        Assert.Equal("Inbound gen4 bzip2 decrypt/decompress is not implemented yet.", ex.Message);
    }

    [Fact]
    public void Gen5InboundBzip2BranchRemainsExplicitlyBlocked()
    {
        var decoder = new InboundPacketDecoder(EncryptionGeneration.Gen5, key: 0);

        var ex = Assert.Throws<NotSupportedException>(() => decoder.DecodeSocketFramePayload([0x06, 1, 2, 3]));

        Assert.Equal("Inbound gen5 bzip2 decrypt/decompress is not implemented yet.", ex.Message);
    }
}

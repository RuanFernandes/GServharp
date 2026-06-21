using Preagonal.GServer.Protocol;
using Xunit;

namespace Preagonal.GServer.Protocol.Tests;

public sealed class GraalWebSocketFrameTests
{
    [Fact]
    public void WrapOutgoingBinarySmallPayloadMatchesGs2libFixture()
    {
        var frame = GraalWebSocketFrame.WrapOutgoingBinary([0x61, 0x62, 0x63]);

        Assert.Equal([0x82, 0x03, 0x61, 0x62, 0x63], frame);
    }

    [Fact]
    public void WrapOutgoingBinaryExtended126PayloadMatchesGs2libFixture()
    {
        var payload = Enumerable.Repeat((byte)0x61, 126).ToArray();

        var frame = GraalWebSocketFrame.WrapOutgoingBinary(payload);

        Assert.Equal([0x82, 0x7E, 0x00, 0x7E], frame.Take(4).ToArray());
        Assert.Equal(payload, frame.Skip(4).ToArray());
    }

    [Fact]
    public void UnwrapIncomingMaskedSmallBinaryPayloadMatchesGs2libFixture()
    {
        var result = GraalWebSocketFrame.UnwrapIncoming([0x82, 0x83, 0x01, 0x02, 0x03, 0x04, 0x60, 0x60, 0x60]);

        Assert.Equal(3, result.Code);
        Assert.Equal([0x61, 0x62, 0x63], result.Payload);
    }

    [Fact]
    public void UnwrapIncomingExtended126UsesAllAvailableBytesLikeGs2lib()
    {
        var result = GraalWebSocketFrame.UnwrapIncoming([
            0x82, 0xFE, 0x00, 0x03, 0x01, 0x02, 0x03, 0x04, 0x60, 0x60, 0x60, 0x65
        ]);

        Assert.Equal(4, result.Code);
        Assert.Equal([0x61, 0x62, 0x63, 0x61], result.Payload);
    }

    [Fact]
    public void UnwrapIncomingCloseFrameMatchesSignedCharGs2libFixture()
    {
        var frame = new byte[] { 0x88, 0x80, 0x00, 0x00, 0x00, 0x00 };

        var result = GraalWebSocketFrame.UnwrapIncoming(frame);

        Assert.Equal(-1, result.Code);
        Assert.Equal(frame, result.Payload);
    }
}

using System.Text;
using GServ.Protocol;
using Xunit;

namespace GServ.Protocol.Tests;

public sealed class OutboundLoginPacketTests
{
    [Fact]
    public void SignaturePacketIsPloSignatureWithGChar73()
    {
        Assert.Equal(new byte[] { 57, 105 }, OutboundLoginPackets.Signature());
    }

    [Fact]
    public void Unknown168PacketUsesConfirmedGCharPacketId()
    {
        Assert.Equal(new byte[] { 200 }, OutboundLoginPackets.Unknown168());
        Assert.Equal(new byte[] { 200, 10 }, OutboundLoginPackets.Unknown168(appendNewline: true));
    }

    [Fact]
    public void DisconnectMessageUsesPloDiscmessageThenRawText()
    {
        Assert.Equal(
            new byte[] { 48, (byte)'N', (byte)'o' },
            OutboundLoginPackets.DisconnectMessage("No"));
    }
}

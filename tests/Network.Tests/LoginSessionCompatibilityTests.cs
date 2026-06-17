using GServ.Network;
using GServ.Protocol;
using Xunit;

namespace GServ.Network.Tests;

public sealed class LoginSessionCompatibilityTests
{
    [Fact]
    public void UnknownClientTypeQueuesCppDisconnectMessageWithNewlineAndRejectsLogin()
    {
        var session = new ClientSessionSkeleton(12);
        var packet = new GraalBinaryWriter();
        packet.WriteGChar(9);

        var accepted = session.ReceiveLoginPacket(packet.ToArray());

        Assert.False(accepted);
        Assert.Equal(SessionLifecycle.Rejected, session.Lifecycle);
        Assert.Equal(
            OutboundLoginPackets.DisconnectMessage(
                "Your client type is unknown.  Please inform the OpenGraal Team.  Type: 512.",
                appendNewline: true),
            session.TakeOutboundBytes());
    }
}

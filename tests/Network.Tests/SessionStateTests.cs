using GServ.Network;
using GServ.Protocol;
using Xunit;

namespace GServ.Network.Tests;

public sealed class SessionStateTests
{
    [Fact]
    public void NewSessionStartsAwaitingLoginLikePltypeAwait()
    {
        var session = new ClientSessionSkeleton(7);

        Assert.Equal(7, session.Id);
        Assert.Equal(PlayerSessionType.Await, session.Type);
        Assert.Equal(SessionLifecycle.AwaitingLoginPrelude, session.Lifecycle);
    }

    [Fact]
    public void ConfirmedClientPreludeSelectsGen2ButDoesNotAuthenticate()
    {
        var session = new ClientSessionSkeleton(7);
        var packet = new GraalBinaryWriter();
        packet.WriteGChar(0);

        session.ReceiveLoginPrelude(packet.ToArray());

        Assert.Equal(PlayerSessionType.Client, session.Type);
        Assert.Equal(EncryptionGeneration.Gen2, session.InboundEncryptionGeneration);
        Assert.Equal(SessionLifecycle.LoginPreludeParsed, session.Lifecycle);
    }
}

using Preagonal.GServer.Network;
using Preagonal.GServer.Protocol;
using Xunit;

namespace Preagonal.GServer.Network.Tests;

public sealed class PreWorldAuthBoundaryTests
{
    [Fact]
    public void FullServerRejectsBeforeServerListRequest()
    {
        var session = Client3Session();
        var boundary = new PreWorldAuthBoundary(new PreWorldAuthOptions(
            MaxPlayers: 1,
            CurrentPlayerCount: 1,
            IsIpBanned: false,
            IsServerListConnected: true,
            AllowedVersions: ["G3D0311C"],
            AllowedVersionText: "6.037"));

        var result = boundary.Begin(session);

        Assert.False(result.Accepted);
        Assert.Equal(SessionLifecycle.Rejected, session.Lifecycle);
        Assert.Equal(
            OutboundLoginPackets.DisconnectMessage("This server has reached its player limit.", appendNewline: true),
            session.TakeOutboundBytes());
        Assert.Empty(result.ServerListRequest);
    }

    [Fact]
    public void DisallowedClientVersionRejectsWithAllowedVersionText()
    {
        var session = Client3Session();
        var boundary = new PreWorldAuthBoundary(new PreWorldAuthOptions(
            MaxPlayers: 128,
            CurrentPlayerCount: 0,
            IsIpBanned: false,
            IsServerListConnected: true,
            AllowedVersions: ["GNW01113:GNW28015"],
            AllowedVersionText: "2.21 - 2.31"));

        var result = boundary.Begin(session);

        Assert.False(result.Accepted);
        Assert.Equal(
            OutboundLoginPackets.DisconnectMessage(
                "Your client version is not allowed on this server.\rAllowed: 2.21 - 2.31",
                appendNewline: true),
            session.TakeOutboundBytes());
    }

    [Fact]
    public void ConnectedServerListReceivesVerifyAccountRequestAndSessionWaits()
    {
        var session = Client3Session();
        var boundary = new PreWorldAuthBoundary(new PreWorldAuthOptions(
            MaxPlayers: 128,
            CurrentPlayerCount: 0,
            IsIpBanned: false,
            IsServerListConnected: true,
            AllowedVersions: ["G3D0311C"],
            AllowedVersionText: "6.037"));

        var result = boundary.Begin(session);

        Assert.True(result.Accepted);
        Assert.Equal(SessionLifecycle.WaitingForServerListAuth, session.Lifecycle);
        Assert.Equal(
            ServerListAuthPackets.VerifyAccount2Request("Ruan", "pw", 7, PlayerSessionType.Client3, "win"),
            result.ServerListRequest);
    }

    [Fact]
    public void ServerListFailureMessageDisconnectsWithThatMessage()
    {
        var session = Client3Session();
        var response = new ServerListVerifyAccount2Response("pc:Ruan", 7, PlayerSessionType.Client3, "Bad password.");

        var accepted = session.ReceiveServerListAuthResponse(response);

        Assert.False(accepted);
        Assert.Equal("pc:Ruan", session.LoginPacket!.AccountName);
        Assert.Equal(SessionLifecycle.Rejected, session.Lifecycle);
        Assert.Equal(
            OutboundLoginPackets.DisconnectMessage("Bad password.", appendNewline: true),
            session.TakeOutboundBytes());
    }

    [Fact]
    public void ServerListSuccessStopsAtPreWorldSendLoginBoundary()
    {
        var session = Client3Session();
        var response = new ServerListVerifyAccount2Response("pc:Ruan", 7, PlayerSessionType.Client3, "SUCCESS");

        var accepted = session.ReceiveServerListAuthResponse(response);

        Assert.True(accepted);
        Assert.Equal("pc:Ruan", session.LoginPacket!.AccountName);
        Assert.Equal(SessionLifecycle.ServerListAuthAcceptedPreWorld, session.Lifecycle);
        Assert.Empty(session.TakeOutboundBytes());
    }

    private static ClientSessionSkeleton Client3Session()
    {
        var session = new ClientSessionSkeleton(7);
        var packet = new GraalBinaryWriter();
        packet.WriteGChar(5);
        packet.WriteGChar(42);
        packet.WriteBytes("G3D0311C"u8);
        packet.WriteGChar(4);
        packet.WriteBytes("Ruan"u8);
        packet.WriteGChar(2);
        packet.WriteBytes("pw"u8);
        packet.WriteBytes("win"u8);
        Assert.True(session.ReceiveLoginPacket(packet.ToArray()));
        return session;
    }
}

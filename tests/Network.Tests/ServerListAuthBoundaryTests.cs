using Preagonal.GServer.Network;
using Preagonal.GServer.Protocol;
using Xunit;

namespace Preagonal.GServer.Network.Tests;

public sealed class ServerListAuthBoundaryTests
{
    [Fact]
    public void BeginQueuesVerifyAccountRequestToGatewayAndWaitsForListServer()
    {
        var session = Client3Session();
        var gateway = new CapturingGateway(isConnected: true);
        var boundary = new ServerListAuthBoundary(gateway, new PreWorldAuthOptions(
            MaxPlayers: 128,
            CurrentPlayerCount: 0,
            IsIpBanned: false,
            IsServerListConnected: gateway.IsConnected,
            AllowedVersions: ["G3D0311C"],
            AllowedVersionText: "6.037"));

        var result = boundary.Begin(session);

        Assert.True(result.Accepted);
        Assert.Equal(SessionLifecycle.WaitingForServerListAuth, session.Lifecycle);
        Assert.Equal(
            ServerListAuthPackets.VerifyAccount2Request("Ruan", "pw", 7, PlayerSessionType.Client3, "win"),
            gateway.LastLoginPacketForPlayer);
        Assert.Empty(session.TakeOutboundBytes());
    }

    [Fact]
    public void BeginDoesNotQueueGatewayPacketWhenPreWorldCheckRejects()
    {
        var session = Client3Session();
        var gateway = new CapturingGateway(isConnected: false);
        var boundary = new ServerListAuthBoundary(gateway, new PreWorldAuthOptions(
            MaxPlayers: 128,
            CurrentPlayerCount: 0,
            IsIpBanned: false,
            IsServerListConnected: gateway.IsConnected,
            AllowedVersions: ["G3D0311C"],
            AllowedVersionText: "6.037"));

        var result = boundary.Begin(session);

        Assert.False(result.Accepted);
        Assert.Null(gateway.LastLoginPacketForPlayer);
        Assert.Equal(
            OutboundLoginPackets.DisconnectMessage("The login server is offline.  Try again later.", appendNewline: true),
            session.TakeOutboundBytes());
    }

    [Fact]
    public void HandleVerifyAccount2SuccessOverwritesAccountAndMovesSessionToPreWorldBoundary()
    {
        var session = BeginPendingServerListAuth();
        var handler = new ServerListAuthResponseHandler((id, type) =>
            id == 7 && type == PlayerSessionType.Client3 ? session : null);

        var result = handler.HandleVerifyAccount2(VerifyAccount2Payload(
            "pc:Ruan",
            7,
            PlayerSessionType.Client3,
            "SUCCESS"));

        Assert.Equal(ServerListAuthResponseStatus.AcceptedPreWorld, result.Status);
        Assert.True(result.SessionFound);
        Assert.Equal("pc:Ruan", session.LoginPacket?.AccountName);
        Assert.Equal(SessionLifecycle.ServerListAuthAcceptedPreWorld, session.Lifecycle);
        Assert.Empty(session.TakeOutboundBytes());
    }

    [Fact]
    public void HandleVerifyAccount2FailureQueuesDisconnectAndRejectsSession()
    {
        var session = BeginPendingServerListAuth();
        var handler = new ServerListAuthResponseHandler((id, type) =>
            id == 7 && type == PlayerSessionType.Client3 ? session : null);

        var result = handler.HandleVerifyAccount2(VerifyAccount2Payload(
            "Ruan",
            7,
            PlayerSessionType.Client3,
            "Bad password."));

        Assert.Equal(ServerListAuthResponseStatus.Rejected, result.Status);
        Assert.True(result.SessionFound);
        Assert.Equal(SessionLifecycle.Rejected, session.Lifecycle);
        Assert.Equal(
            OutboundLoginPackets.DisconnectMessage("Bad password.", appendNewline: true),
            session.TakeOutboundBytes());
    }

    [Fact]
    public void HandleVerifyAccount2MissingSessionDoesNothing()
    {
        var handler = new ServerListAuthResponseHandler((_, _) => null);

        var result = handler.HandleVerifyAccount2(VerifyAccount2Payload(
            "Ruan",
            7,
            PlayerSessionType.Client3,
            "SUCCESS"));

        Assert.Equal(ServerListAuthResponseStatus.SessionNotFound, result.Status);
        Assert.False(result.SessionFound);
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

    private static ClientSessionSkeleton BeginPendingServerListAuth()
    {
        var session = Client3Session();
        var gateway = new CapturingGateway(isConnected: true);
        var boundary = new ServerListAuthBoundary(gateway, new PreWorldAuthOptions(
            MaxPlayers: 128,
            CurrentPlayerCount: 0,
            IsIpBanned: false,
            IsServerListConnected: gateway.IsConnected,
            AllowedVersions: ["G3D0311C"],
            AllowedVersionText: "6.037"));

        var result = boundary.Begin(session);
        Assert.True(result.Accepted);
        Assert.Equal(SessionLifecycle.WaitingForServerListAuth, session.Lifecycle);
        return session;
    }

    private static byte[] VerifyAccount2Payload(
        string account,
        ushort id,
        PlayerSessionType type,
        string message)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)account.Length);
        packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes(account));
        packet.WriteGShort(id);
        packet.WriteGChar((byte)type);
        packet.WriteBytes(System.Text.Encoding.ASCII.GetBytes(message));
        return packet.ToArray();
    }

    private sealed class CapturingGateway(bool isConnected) : IServerListGateway
    {
        public bool IsConnected { get; } = isConnected;
        public byte[]? LastLoginPacketForPlayer { get; private set; }

        public void SendLoginPacketForPlayer(byte[] packetBody)
        {
            LastLoginPacketForPlayer = packetBody;
        }

        public void SendPlayerAdd(byte[] packetBody)
        {
            LastLoginPacketForPlayer = packetBody;
        }

        public void SendPlayerRemove(byte[] packetBody)
        {
            LastLoginPacketForPlayer = packetBody;
        }

        public void SendServerInfoForPlayer(byte[] packetBody)
        {
            LastLoginPacketForPlayer = packetBody;
        }
    }
}

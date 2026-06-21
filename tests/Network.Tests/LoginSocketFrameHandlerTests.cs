using Preagonal.GServer.Protocol;
using Xunit;

namespace Preagonal.GServer.Network.Tests;

public sealed class LoginSocketFrameHandlerTests
{
    [Fact]
    public async Task HandleFrameAsyncSendsLoginVerificationAndKeepsClientConnectedWaitingForListServer()
    {
        var gateway = new RecordingGateway { IsConnected = true };
        var bridge = new LoginAuthBridge(gateway, AuthOptions());
        var handler = new LoginSocketFrameHandler(bridge);

        var result = await handler.HandleFrameAsync(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            Client3LoginPacket(),
            CancellationToken.None);

        Assert.True(result.ContinueSession);
        Assert.Empty(result.OutboundBytes);
        Assert.Equal(
            ServerListAuthPackets.VerifyAccount2Request("Ruan", "pw", 7, PlayerSessionType.Client3, "win"),
            Assert.Single(gateway.SentPackets));
    }

    [Fact]
    public async Task HandleFrameAsyncWritesImmediateRejectBytesAndStopsWhenServerListIsOffline()
    {
        var gateway = new RecordingGateway { IsConnected = false };
        var bridge = new LoginAuthBridge(gateway, AuthOptions());
        var handler = new LoginSocketFrameHandler(bridge);

        var result = await handler.HandleFrameAsync(
            new ClientSocketSessionContext(7, "127.0.0.1"),
            Client3LoginPacket(),
            CancellationToken.None);

        Assert.False(result.ContinueSession);
        Assert.Equal(
            SocketFrame(OutboundLoginPackets.DisconnectMessage("The login server is offline.  Try again later.", appendNewline: true), 42),
            result.OutboundBytes);
    }

    private static PreWorldAuthOptions AuthOptions() =>
        new(
            MaxPlayers: 128,
            CurrentPlayerCount: 0,
            IsIpBanned: false,
            IsServerListConnected: true,
            AllowedVersions: ["G3D0311C"],
            AllowedVersionText: "6.037");

    private static byte[] Client3LoginPacket()
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar(5);
        packet.WriteGChar(42);
        packet.WriteBytes("G3D0311C"u8);
        packet.WriteGChar(4);
        packet.WriteBytes("Ruan"u8);
        packet.WriteGChar(2);
        packet.WriteBytes("pw"u8);
        packet.WriteBytes("win"u8);
        return packet.ToArray();
    }

    private static byte[] SocketFrame(byte[] raw, byte key)
    {
        var queue = new GraalFileQueue();
        queue.SetCodec(EncryptionGeneration.Gen5, key);
        queue.AddPacket(raw);
        return queue.FlushSocket(forceSendFiles: true);
    }

    private sealed class RecordingGateway : IServerListGateway
    {
        public bool IsConnected { get; init; }
        public List<byte[]> SentPackets { get; } = [];

        public void SendLoginPacketForPlayer(byte[] packetBody)
        {
            SentPackets.Add(packetBody);
        }

        public void SendPlayerAdd(byte[] packetBody)
        {
            SentPackets.Add(packetBody);
        }

        public void SendPlayerRemove(byte[] packetBody)
        {
            SentPackets.Add(packetBody);
        }

        public void SendServerInfoForPlayer(byte[] packetBody)
        {
            SentPackets.Add(packetBody);
        }
    }
}

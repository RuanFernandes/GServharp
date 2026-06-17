using GServ.Protocol;
using Xunit;

namespace GServ.Network.Tests;

public sealed class ProductionLoginSocketFrameHandlerTests
{
    [Fact]
    public async Task HandleFrameAsyncSendsLoginVerificationAndKeepsClientConnectedWaitingForListServer()
    {
        var gateway = new RecordingGateway { IsConnected = true };
        var bridge = new ProductionLoginAuthBridge(gateway, AuthOptions());
        var handler = new ProductionLoginSocketFrameHandler(bridge);

        var result = await handler.HandleFrameAsync(
            new ProductionSocketSessionContext(7, "127.0.0.1"),
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
        var bridge = new ProductionLoginAuthBridge(gateway, AuthOptions());
        var handler = new ProductionLoginSocketFrameHandler(bridge);

        var result = await handler.HandleFrameAsync(
            new ProductionSocketSessionContext(7, "127.0.0.1"),
            Client3LoginPacket(),
            CancellationToken.None);

        Assert.False(result.ContinueSession);
        Assert.Equal(
            OutboundLoginPackets.DisconnectMessage("The login server is offline.  Try again later.", appendNewline: true),
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

    private sealed class RecordingGateway : IProductionServerListGateway
    {
        public bool IsConnected { get; init; }
        public List<byte[]> SentPackets { get; } = [];

        public void SendLoginPacketForPlayer(byte[] packetBody)
        {
            SentPackets.Add(packetBody);
        }
    }
}

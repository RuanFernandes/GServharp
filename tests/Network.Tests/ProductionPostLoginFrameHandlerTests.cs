using GServ.Game;
using GServ.Protocol;
using Xunit;

namespace GServ.Network.Tests;

public sealed class ProductionPostLoginFrameHandlerTests
{
    [Fact]
    public async Task HandleFrameAsyncDecodesAndDispatchesConfirmedPlayerProps()
    {
        var player = new RuntimePlayer(2, "pc:Ruan", RuntimePlayerKind.Client);
        var log = new List<string>();
        var handler = new ProductionPostLoginFrameHandler(
            player,
            EncryptionGeneration.Gen1,
            key: 0,
            log.Add);

        var result = await handler.HandleFrameAsync(
            new ProductionSocketSessionContext(2, "127.0.0.1"),
            WithNewline(PlayerPropsPacket(PlayerPropertyId.X, 70, PlayerPropertyId.Y, 71)),
            CancellationToken.None);

        Assert.True(result.ContinueSession);
        Assert.Empty(result.OutboundBytes);
        Assert.Equal(560, player.PixelX);
        Assert.Equal(568, player.PixelY);
        Assert.Contains(log, line => line.Contains("Applied confirmed PLI_PLAYERPROPS subset", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HandleFrameAsyncStopsAndLogsAssignedButUnimplementedPacket()
    {
        var player = new RuntimePlayer(2, "pc:Ruan", RuntimePlayerKind.Client);
        var log = new List<string>();
        var handler = new ProductionPostLoginFrameHandler(
            player,
            EncryptionGeneration.Gen1,
            key: 0,
            log.Add);

        var result = await handler.HandleFrameAsync(
            new ProductionSocketSessionContext(2, "127.0.0.1"),
            WithNewline(Packet(4, 1, 2, 3)),
            CancellationToken.None);

        Assert.False(result.ContinueSession);
        Assert.Empty(result.OutboundBytes);
        Assert.Contains(log, line => line.Contains("PLI_BOMBADD", StringComparison.Ordinal));
        Assert.Contains(log, line => line.Contains("blocked", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task HandleFrameAsyncStopsWithDisconnectBytesAfterSixUnassignedPackets()
    {
        var player = new RuntimePlayer(2, "pc:Ruan", RuntimePlayerKind.Client);
        var handler = new ProductionPostLoginFrameHandler(
            player,
            EncryptionGeneration.Gen1,
            key: 0,
            _ => { });
        var frame = new List<byte>();
        for (var i = 0; i < 6; i++)
        {
            frame.AddRange(Packet(25, 1, 2, 3));
            frame.Add((byte)'\n');
        }

        var result = await handler.HandleFrameAsync(
            new ProductionSocketSessionContext(2, "127.0.0.1"),
            frame.ToArray(),
            CancellationToken.None);

        Assert.False(result.ContinueSession);
        Assert.Equal(
            OutboundLoginPackets.DisconnectMessage("Disconnected for sending invalid packets.", appendNewline: true),
            result.OutboundBytes);
    }

    private static byte[] PlayerPropsPacket(PlayerPropertyId first, byte firstValue, PlayerPropertyId second, byte secondValue)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar((byte)PlayerToServerPacketId.PlayerProps);
        packet.WriteGChar((byte)first);
        packet.WriteGChar(firstValue);
        packet.WriteGChar((byte)second);
        packet.WriteGChar(secondValue);
        return packet.ToArray();
    }

    private static byte[] Packet(byte id, params byte[] body)
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar(id);
        packet.WriteBytes(body);
        return packet.ToArray();
    }

    private static byte[] WithNewline(byte[] packet) =>
    [
        ..packet,
        (byte)'\n'
    ];
}

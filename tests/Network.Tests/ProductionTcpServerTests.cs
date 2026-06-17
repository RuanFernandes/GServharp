using System.Net;
using System.Net.Sockets;
using GServ.Game;
using GServ.Network;
using GServ.Protocol;
using Xunit;

namespace GServ.Network.Tests;

public sealed class ProductionTcpServerTests
{
    [Fact]
    public async Task AcceptOneAsyncAssignsPlayerIdFromCppInitialValueAndDispatchesBufferedFrames()
    {
        var handler = new RecordingProductionFrameHandler(expectedFrames: 2);
        using var server = new ProductionTcpServer(IPAddress.Loopback, port: 0, handler);
        server.Start();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var acceptTask = server.AcceptOneAsync(timeout.Token);

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
        await using var stream = client.GetStream();

        await stream.WriteAsync(new byte[] { 0, 3, 65 }, timeout.Token);
        await stream.WriteAsync(new byte[] { 66, 67, 0, 1, 68 }, timeout.Token);
        await handler.WaitForExpectedFrames(timeout.Token);
        client.Close();

        var result = await acceptTask;

        Assert.Equal(2, result.PlayerId);
        Assert.Equal(2, handler.Sessions.Single());
        Assert.Equal([new byte[] { 65, 66, 67 }, new byte[] { 68 }], handler.Frames);
        Assert.Equal(ProductionTcpSessionStopReason.ClientDisconnected, result.StopReason);
    }

    [Fact]
    public async Task AcceptOneAsyncWritesHandlerOutboundBytesAfterFrameDispatch()
    {
        var handler = new EchoProductionFrameHandler();
        using var server = new ProductionTcpServer(IPAddress.Loopback, port: 0, handler);
        server.Start();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var acceptTask = server.AcceptOneAsync(timeout.Token);

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
        await using var stream = client.GetStream();
        await stream.WriteAsync(new byte[] { 0, 1, 70 }, timeout.Token);

        var response = new byte[1];
        var read = await stream.ReadAsync(response, timeout.Token);
        client.Close();
        var result = await acceptTask;

        Assert.Equal(1, read);
        Assert.Equal(71, response[0]);
        Assert.Equal(ProductionTcpSessionStopReason.ClientDisconnected, result.StopReason);
    }

    [Fact]
    public async Task AcceptOneAsyncCanDriveConfirmedPostLoginPlayerPropsDispatch()
    {
        var player = new RuntimePlayer(2, "pc:Ruan", RuntimePlayerKind.Client);
        var handler = new ProductionPostLoginFrameHandler(player, EncryptionGeneration.Gen1, key: 0);
        using var server = new ProductionTcpServer(IPAddress.Loopback, port: 0, handler);
        server.Start();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var acceptTask = server.AcceptOneAsync(timeout.Token);

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
        await using var stream = client.GetStream();
        await stream.WriteAsync(LengthFrame(WithNewline(PlayerPropsPacket(
            PlayerPropertyId.X,
            70,
            PlayerPropertyId.Y,
            71))), timeout.Token);
        client.Close();

        var result = await acceptTask;

        Assert.Equal(ProductionTcpSessionStopReason.ClientDisconnected, result.StopReason);
        Assert.Equal(560, player.PixelX);
        Assert.Equal(568, player.PixelY);
    }

    [Fact]
    public async Task AcceptOneAsyncWritesInvalidPacketDisconnectBytesAndStopsWhenHandlerStops()
    {
        var player = new RuntimePlayer(2, "pc:Ruan", RuntimePlayerKind.Client);
        var handler = new ProductionPostLoginFrameHandler(player, EncryptionGeneration.Gen1, key: 0);
        using var server = new ProductionTcpServer(IPAddress.Loopback, port: 0, handler);
        server.Start();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var acceptTask = server.AcceptOneAsync(timeout.Token);

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
        await using var stream = client.GetStream();
        var frame = new List<byte>();
        for (var i = 0; i < 6; i++)
        {
            frame.AddRange(Packet(25, 1, 2, 3));
            frame.Add((byte)'\n');
        }

        await stream.WriteAsync(LengthFrame(frame.ToArray()), timeout.Token);

        var expected = OutboundLoginPackets.DisconnectMessage("Disconnected for sending invalid packets.", appendNewline: true);
        var received = new byte[expected.Length];
        var read = await stream.ReadAsync(received, timeout.Token);
        var result = await acceptTask;

        Assert.Equal(expected.Length, read);
        Assert.Equal(expected, received);
        Assert.Equal(ProductionTcpSessionStopReason.HandlerStopped, result.StopReason);
    }

    private sealed class RecordingProductionFrameHandler(int expectedFrames) : IProductionSocketFrameHandler
    {
        private readonly TaskCompletionSource _expectedFramesSeen = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<ushort> Sessions { get; } = [];
        public List<byte[]> Frames { get; } = [];

        public ValueTask<ProductionSocketFrameResult> HandleFrameAsync(
            ProductionSocketSessionContext session,
            ReadOnlyMemory<byte> frame,
            CancellationToken cancellationToken)
        {
            if (!Sessions.Contains(session.PlayerId))
                Sessions.Add(session.PlayerId);

            Frames.Add(frame.ToArray());
            if (Frames.Count == expectedFrames)
                _expectedFramesSeen.TrySetResult();

            return ValueTask.FromResult(ProductionSocketFrameResult.Continue());
        }

        public async Task WaitForExpectedFrames(CancellationToken cancellationToken)
        {
            await using var registration = cancellationToken.Register(() => _expectedFramesSeen.TrySetCanceled(cancellationToken));
            await _expectedFramesSeen.Task;
        }
    }

    private sealed class EchoProductionFrameHandler : IProductionSocketFrameHandler
    {
        public ValueTask<ProductionSocketFrameResult> HandleFrameAsync(
            ProductionSocketSessionContext session,
            ReadOnlyMemory<byte> frame,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(ProductionSocketFrameResult.Continue([unchecked((byte)(frame.Span[0] + 1))]));
        }
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

    private static byte[] LengthFrame(byte[] packet) =>
    [
        (byte)(packet.Length >> 8),
        (byte)packet.Length,
        ..packet
    ];
}

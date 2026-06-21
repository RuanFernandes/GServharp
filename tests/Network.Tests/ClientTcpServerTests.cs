using System.Net;
using System.Net.Sockets;
using Preagonal.GServer.Game;
using Preagonal.GServer.Network;
using Preagonal.GServer.Protocol;
using Xunit;

namespace Preagonal.GServer.Network.Tests;

public sealed class ClientTcpServerTests
{
    [Fact]
    public async Task AcceptOneAsyncAssignsPlayerIdFromCppInitialValueAndDispatchesBufferedFrames()
    {
        var handler = new RecordingFrameHandler(expectedFrames: 2);
        using var server = new ClientTcpServer(IPAddress.Loopback, port: 0, handler);
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
        Assert.Equal(ClientTcpSessionStopReason.ClientDisconnected, result.StopReason);
    }

    [Fact]
    public async Task AcceptOneAsyncWritesHandlerOutboundBytesAfterFrameDispatch()
    {
        var handler = new EchoFrameHandler();
        using var server = new ClientTcpServer(IPAddress.Loopback, port: 0, handler);
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
        Assert.Equal(ClientTcpSessionStopReason.ClientDisconnected, result.StopReason);
    }

    [Fact]
    public async Task AnyAddressListenerAcceptsIpv4Clients()
    {
        var handler = new RecordingFrameHandler(expectedFrames: 1);
        using var server = new ClientTcpServer(IPAddress.Any, port: 0, handler);
        server.Start();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var acceptTask = server.AcceptOneAsync(timeout.Token);

        using var client = new TcpClient(AddressFamily.InterNetwork);
        await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
        await using var stream = client.GetStream();
        await stream.WriteAsync(new byte[] { 0, 1, 70 }, timeout.Token);
        await handler.WaitForExpectedFrames(timeout.Token);
        client.Close();

        var result = await acceptTask;

        Assert.Equal(ClientTcpSessionStopReason.ClientDisconnected, result.StopReason);
    }

    [Fact]
    public async Task RunAsyncLogsAcceptBeforeRead()
    {
        var accepted = new TaskCompletionSource<ClientSocketSessionContext>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new RecordingFrameHandler(expectedFrames: 1);
        using var server = new ClientTcpServer(
            IPAddress.Loopback,
            port: 0,
            handler,
            accepted: session => accepted.TrySetResult(session));
        server.Start();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var serverStop = new CancellationTokenSource();
        var runTask = server.RunAsync(serverStop.Token);

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);

        var session = await accepted.Task.WaitAsync(timeout.Token);
        Assert.Equal(2, session.PlayerId);
        Assert.Empty(handler.Frames);

        client.Close();
        serverStop.Cancel();
        await runTask.WaitAsync(timeout.Token);
    }

    [Fact]
    public async Task AcceptOneAsyncRegistersClientConnectionUntilSessionEnds()
    {
        var handler = new RecordingFrameHandler(expectedFrames: 1);
        var registry = new TcpClientConnectionRegistry();
        using var server = new ClientTcpServer(IPAddress.Loopback, port: 0, handler, registry);
        server.Start();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var acceptTask = server.AcceptOneAsync(timeout.Token);

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
        await using var stream = client.GetStream();
        await stream.WriteAsync(new byte[] { 0, 1, 70 }, timeout.Token);
        await handler.WaitForExpectedFrames(timeout.Token);

        Assert.True(await registry.SendAsync(2, new byte[] { 80 }, timeout.Token));
        var received = new byte[1];
        await stream.ReadExactlyAsync(received, timeout.Token);
        Assert.Equal([80], received);

        client.Close();
        _ = await acceptTask;
        Assert.False(await registry.SendAsync(2, new byte[] { 81 }, timeout.Token));
    }

    [Fact]
    public async Task AcceptOneAsyncCanDriveConfirmedPostLoginPlayerPropsDispatch()
    {
        var player = new RuntimePlayer(2, "pc:Ruan", RuntimePlayerKind.Client);
        var handler = new PostLoginFrameHandler(player, EncryptionGeneration.Gen1, key: 0);
        using var server = new ClientTcpServer(IPAddress.Loopback, port: 0, handler);
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

        Assert.Equal(ClientTcpSessionStopReason.ClientDisconnected, result.StopReason);
        Assert.Equal(560, player.PixelX);
        Assert.Equal(568, player.PixelY);
    }

    [Fact]
    public async Task AcceptOneAsyncWritesInvalidPacketDisconnectBytesAndStopsWhenHandlerStops()
    {
        var player = new RuntimePlayer(2, "pc:Ruan", RuntimePlayerKind.Client);
        var handler = new PostLoginFrameHandler(player, EncryptionGeneration.Gen1, key: 0);
        using var server = new ClientTcpServer(IPAddress.Loopback, port: 0, handler);
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
        Assert.Equal(ClientTcpSessionStopReason.HandlerStopped, result.StopReason);
    }

    private sealed class RecordingFrameHandler(int expectedFrames) : IClientSocketFrameHandler
    {
        private readonly TaskCompletionSource _expectedFramesSeen = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<ushort> Sessions { get; } = [];
        public List<byte[]> Frames { get; } = [];

        public ValueTask<ClientSocketFrameResult> HandleFrameAsync(
            ClientSocketSessionContext session,
            ReadOnlyMemory<byte> frame,
            CancellationToken cancellationToken)
        {
            if (!Sessions.Contains(session.PlayerId))
                Sessions.Add(session.PlayerId);

            Frames.Add(frame.ToArray());
            if (Frames.Count == expectedFrames)
                _expectedFramesSeen.TrySetResult();

            return ValueTask.FromResult(ClientSocketFrameResult.Continue());
        }

        public async Task WaitForExpectedFrames(CancellationToken cancellationToken)
        {
            await using var registration = cancellationToken.Register(() => _expectedFramesSeen.TrySetCanceled(cancellationToken));
            await _expectedFramesSeen.Task;
        }
    }

    private sealed class EchoFrameHandler : IClientSocketFrameHandler
    {
        public ValueTask<ClientSocketFrameResult> HandleFrameAsync(
            ClientSocketSessionContext session,
            ReadOnlyMemory<byte> frame,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(ClientSocketFrameResult.Continue([unchecked((byte)(frame.Span[0] + 1))]));
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

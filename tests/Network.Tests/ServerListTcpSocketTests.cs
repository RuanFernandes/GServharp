using System.Net;
using System.Net.Sockets;
using System.IO.Compression;
using Preagonal.GServer.Protocol;
using Xunit;

namespace Preagonal.GServer.Network.Tests;

public sealed class ServerListTcpSocketTests
{
    [Fact]
    public async Task SendPacketWritesConfirmedGen1AndGen2ListServerFramesToTcpStream()
    {
        using var listener = new TcpListener(IPAddress.Loopback, port: 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port.ToString();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var socket = new ServerListTcpSocket();

        Assert.True(socket.Initialize(IPAddress.Loopback.ToString(), port));
        var acceptTask = listener.AcceptTcpClientAsync(timeout.Token);
        Assert.True(socket.Connect());
        using var serverSide = await acceptTask;
        await using var stream = serverSide.GetStream();

        socket.SetCodec(EncryptionGeneration.Gen1, key: 0);
        socket.SendPacket(ServerListAuthPackets.RegisterV3("3.0.9-beta"), sendNow: true);

        var gen1 = new byte[12];
        await stream.ReadExactlyAsync(gen1, timeout.Token);
        Assert.Equal(">" + "3.0.9-beta\n", System.Text.Encoding.ASCII.GetString(gen1));

        socket.SetCodec(EncryptionGeneration.Gen2, key: 0);
        socket.SendPacket(ServerListAuthPackets.ServerHqPass("secret"));

        var expectedQueue = new GraalFileQueue();
        expectedQueue.SetCodec(EncryptionGeneration.Gen2, key: 0);
        expectedQueue.AddRawPacket("7secret\n"u8);
        var expectedGen2 = expectedQueue.FlushSocket();

        var gen2 = new byte[expectedGen2.Length];
        await stream.ReadExactlyAsync(gen2, timeout.Token);
        Assert.Equal(expectedGen2, gen2);
    }

    [Fact]
    public async Task ReceivePacketsAsyncReturnsDecodedListServerPacketsFromTcpStream()
    {
        using var listener = new TcpListener(IPAddress.Loopback, port: 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port.ToString();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var socket = new ServerListTcpSocket();

        Assert.True(socket.Initialize(IPAddress.Loopback.ToString(), port));
        var acceptTask = listener.AcceptTcpClientAsync(timeout.Token);
        Assert.True(socket.Connect());
        using var serverSide = await acceptTask;
        await using var stream = serverSide.GetStream();

        await stream.WriteAsync(LengthFrame(Zlib("a\nb\n"u8.ToArray())), timeout.Token);

        Assert.Equal(
            ["a"u8.ToArray(), "b"u8.ToArray()],
            await socket.ReceivePacketsAsync(timeout.Token));
    }

    [Fact]
    public void TcpSocketCanSendLoginPacketsThroughServerListGatewayInterface()
    {
        Assert.IsAssignableFrom<IServerListGateway>(new ServerListTcpSocket());
    }

    private static byte[] LengthFrame(byte[] payload) =>
    [
        (byte)(payload.Length >> 8),
        (byte)payload.Length,
        ..payload
    ];

    private static byte[] Zlib(byte[] payload)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(payload);
        return output.ToArray();
    }
}

using System.Net;
using System.Net.Sockets;
using Xunit;

namespace Preagonal.GServer.Network.Tests;

public sealed class TcpClientConnectionRegistryTests
{
    [Fact]
    public async Task SendAsyncWritesBytesToRegisteredPlayerStream()
    {
        using var listener = new TcpListener(IPAddress.Loopback, port: 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var client = new TcpClient();
        var acceptTask = listener.AcceptTcpClientAsync(timeout.Token);
        await client.ConnectAsync(IPAddress.Loopback, port, timeout.Token);
        using var serverSide = await acceptTask;
        await using var serverStream = serverSide.GetStream();
        await using var clientStream = client.GetStream();
        var registry = new TcpClientConnectionRegistry();

        registry.Register(7, serverStream);
        Assert.True(await registry.SendAsync(7, new byte[] { 65, 66 }, timeout.Token));

        var received = new byte[2];
        await clientStream.ReadExactlyAsync(received, timeout.Token);
        Assert.Equal([65, 66], received);

        registry.Unregister(7);
        Assert.False(await registry.SendAsync(7, new byte[] { 67 }, timeout.Token));
    }
}

using GServ.Game;
using GServ.Network;
using GServ.Protocol;
using System.Net;
using System.Net.Sockets;
using Xunit;

namespace GServ.Network.Tests;

public sealed class DevOnlyLocalSessionPipelineTests
{
    [Fact]
    public void ProcessLengthPrefixedLoginRequiresExplicitDevOnlyAuthOptIn()
    {
        using var temp = new TemporaryDirectory();
        var pipeline = new DevOnlyLocalSessionPipeline(
            new DevOnlyLocalServerOptions(EnableDevOnlyAuth: false, LevelName: "start.nw"),
            new NwLevelFileLoader(new IndexedServerFileSystem(temp.Path)));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            pipeline.ProcessLengthPrefixedInput(LengthFrame(Client3LoginPacket())));

        Assert.Equal("Dev-only auth must be explicitly enabled.", ex.Message);
    }

    [Fact]
    public void ProcessLengthPrefixedLoginRunsDevOnlyAuthLoadsNwAndStopsAtSendLevelBoundary()
    {
        using var temp = new TemporaryDirectory();
        var world = Directory.CreateDirectory(Path.Combine(temp.Path, "world"));
        File.WriteAllText(
            Path.Combine(world.FullName, "start.nw"),
            """
            GLEVNW01
            BOARD 0 0 1 0 AB
            LINK next.nw 1 2 3 4 5 6
            SIGN 4 5
            A
            SIGNEND
            CHEST 10 11 redrupee 3
            """);

        var fileSystem = new IndexedServerFileSystem(temp.Path);
        fileSystem.AddDirectory("world", "*.nw");
        var pipeline = new DevOnlyLocalSessionPipeline(
            new DevOnlyLocalServerOptions(EnableDevOnlyAuth: true, LevelName: "start.nw"),
            new NwLevelFileLoader(fileSystem));

        var result = pipeline.ProcessLengthPrefixedInput(LengthFrame(Client3LoginPacket()));

        Assert.True(result.Accepted);
        Assert.Equal(SessionLifecycle.DynamicLevelPayloadSent, result.Lifecycle);
        Assert.Equal(DevOnlyLocalStopPoint.BeforeRuntimeWorldSimulation, result.StopPoint);
        Assert.Contains(result.Log, line => line.Contains("DEV-ONLY auth accepted", StringComparison.Ordinal));
        Assert.Contains(result.Log, line => line.Contains("Loaded .nw level start.nw", StringComparison.Ordinal));
        Assert.Contains(LevelNamePacket("start.nw"), result.OutboundBytes);
        Assert.Contains(new byte[] { 132, 32, 96, 34, 10, 133 }, result.OutboundBytes);
        Assert.Contains(new byte[] { 36, 32, 42, 43, 34, 35, 10 }, result.OutboundBytes);
    }

    [Fact]
    public void ProcessLengthPrefixedLoginReportsMissingDevLevelAsWarpFailedBoundary()
    {
        using var temp = new TemporaryDirectory();
        var fileSystem = new IndexedServerFileSystem(temp.Path);
        fileSystem.AddDirectory("world", "*.nw");
        var pipeline = new DevOnlyLocalSessionPipeline(
            new DevOnlyLocalServerOptions(EnableDevOnlyAuth: true, LevelName: "missing.nw"),
            new NwLevelFileLoader(fileSystem));

        var result = pipeline.ProcessLengthPrefixedInput(LengthFrame(Client3LoginPacket()));

        Assert.False(result.Accepted);
        Assert.Equal(SessionLifecycle.ReadyForLevelWarp, result.Lifecycle);
        Assert.Equal(DevOnlyLocalStopPoint.MissingLevel, result.StopPoint);
        Assert.Contains(
            new byte[] { 47, (byte)'m', (byte)'i', (byte)'s', (byte)'s', (byte)'i', (byte)'n', (byte)'g', (byte)'.', (byte)'n', (byte)'w', 10 },
            result.OutboundBytes);
    }

    [Fact]
    public async Task TcpShellAcceptsOneClientAndWritesPipelineOutput()
    {
        using var temp = new TemporaryDirectory();
        var world = Directory.CreateDirectory(Path.Combine(temp.Path, "world"));
        File.WriteAllText(Path.Combine(world.FullName, "start.nw"), "GLEVNW01\n");

        var fileSystem = new IndexedServerFileSystem(temp.Path);
        fileSystem.AddDirectory("world", "*.nw");
        var pipeline = new DevOnlyLocalSessionPipeline(
            new DevOnlyLocalServerOptions(EnableDevOnlyAuth: true, LevelName: "start.nw"),
            new NwLevelFileLoader(fileSystem));
        using var server = new DevOnlyLocalTcpServer(IPAddress.Loopback, port: 0, pipeline);
        server.Start();

        var serveTask = server.AcceptOneAsync(CancellationToken.None);
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, server.Port);
        await using var stream = client.GetStream();
        await stream.WriteAsync(LengthFrame(Client3LoginPacket()));

        var received = new List<byte>();
        var buffer = new byte[8192];
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var read = await stream.ReadAsync(buffer, timeout.Token);
        received.AddRange(buffer[..read]);

        var result = await serveTask;
        Assert.True(result.Accepted);
        Assert.Contains(LevelNamePacket("start.nw"), received.ToArray());
    }

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

    private static byte[] LengthFrame(byte[] packet) =>
    [
        (byte)(packet.Length >> 8),
        (byte)packet.Length,
        ..packet
    ];

    private static byte[] LevelNamePacket(string levelName) =>
    [
        (byte)((byte)ServerToPlayerPacketId.LevelName + 32),
        ..System.Text.Encoding.ASCII.GetBytes(levelName),
        (byte)'\n'
    ];

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}

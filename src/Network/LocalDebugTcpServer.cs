using System.Net;
using System.Net.Sockets;

namespace Preagonal.GServer.Network;

public sealed class LocalDebugTcpServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly LocalDebugSessionPipeline _pipeline;

    public LocalDebugTcpServer(IPAddress address, int port, LocalDebugSessionPipeline pipeline)
    {
        _listener = new TcpListener(address, port);
        _pipeline = pipeline;
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public void Start() => _listener.Start();

    public async Task<LocalDebugSessionResult> AcceptOneAsync(CancellationToken cancellationToken)
    {
        using var client = await _listener.AcceptTcpClientAsync(cancellationToken);
        await using var stream = client.GetStream();
        var connection = _pipeline.CreateConnection();
        LocalDebugSessionResult? result = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            byte[] input;
            try
            {
                input = await ReadOneLengthPrefixedFrame(stream, cancellationToken);
            }
            catch (EndOfStreamException) when (result is not null)
            {
                break;
            }

            result = connection.ProcessLengthPrefixedInput(input);
            if (result.OutboundBytes.Length != 0)
                await stream.WriteAsync(result.OutboundBytes, cancellationToken);

            if (result.Log.Any(line => line.Contains("Unsupported post-login frame", StringComparison.Ordinal)))
                break;
        }

        return result ?? new LocalDebugSessionResult(
            Accepted: false,
            Lifecycle: SessionLifecycle.Disconnected,
            StopPoint: LocalDebugStopPoint.Rejected,
            OutboundBytes: [],
            Log: ["Client disconnected before sending a length-prefixed frame."]);
    }

    private static async Task<byte[]> ReadOneLengthPrefixedFrame(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var header = new byte[2];
        await ReadExact(stream, header, cancellationToken);
        var length = (header[0] << 8) | header[1];
        var payload = new byte[length];
        await ReadExact(stream, payload, cancellationToken);

        var framed = new byte[length + 2];
        header.CopyTo(framed, 0);
        payload.CopyTo(framed.AsSpan(2));
        return framed;
    }

    private static async Task ReadExact(
        NetworkStream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken);
            if (read == 0)
                throw new EndOfStreamException("Client disconnected before sending a complete length-prefixed frame.");
            offset += read;
        }
    }

    public void Dispose() => _listener.Stop();
}

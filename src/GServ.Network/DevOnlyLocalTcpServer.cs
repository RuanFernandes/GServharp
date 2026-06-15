using System.Net;
using System.Net.Sockets;

namespace GServ.Network;

public sealed class DevOnlyLocalTcpServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly DevOnlyLocalSessionPipeline _pipeline;

    public DevOnlyLocalTcpServer(IPAddress address, int port, DevOnlyLocalSessionPipeline pipeline)
    {
        _listener = new TcpListener(address, port);
        _pipeline = pipeline;
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public void Start() => _listener.Start();

    public async Task<DevOnlyLocalSessionResult> AcceptOneAsync(CancellationToken cancellationToken)
    {
        using var client = await _listener.AcceptTcpClientAsync(cancellationToken);
        await using var stream = client.GetStream();
        var input = await ReadOneLengthPrefixedFrame(stream, cancellationToken);

        var result = _pipeline.ProcessLengthPrefixedInput(input);
        if (result.OutboundBytes.Length != 0)
            await stream.WriteAsync(result.OutboundBytes, cancellationToken);

        return result;
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

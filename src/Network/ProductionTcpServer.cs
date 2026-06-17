using System.Net;
using System.Net.Sockets;

namespace GServ.Network;

public sealed record ProductionSocketSessionContext(ushort PlayerId, string RemoteAddress);

public sealed record ProductionSocketFrameResult(bool ContinueSession, byte[] OutboundBytes)
{
    public static ProductionSocketFrameResult Continue(byte[]? outboundBytes = null) =>
        new(true, outboundBytes ?? []);

    public static ProductionSocketFrameResult Stop(byte[]? outboundBytes = null) =>
        new(false, outboundBytes ?? []);
}

public interface IProductionSocketFrameHandler
{
    ValueTask<ProductionSocketFrameResult> HandleFrameAsync(
        ProductionSocketSessionContext session,
        ReadOnlyMemory<byte> frame,
        CancellationToken cancellationToken);
}

public enum ProductionTcpSessionStopReason
{
    ClientDisconnected,
    HandlerStopped
}

public sealed record ProductionTcpSessionResult(
    ushort PlayerId,
    ProductionTcpSessionStopReason StopReason);

public sealed class ProductionTcpServer : IDisposable
{
    private const ushort PlayerIdInitialValue = 2;

    private readonly TcpListener _listener;
    private readonly IProductionSocketFrameHandler _handler;
    private ushort _nextPlayerId = PlayerIdInitialValue;

    public ProductionTcpServer(IPAddress address, int port, IProductionSocketFrameHandler handler)
    {
        _listener = new TcpListener(address, port);
        _handler = handler;
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public void Start() => _listener.Start();

    public async Task<ProductionTcpSessionResult> AcceptOneAsync(CancellationToken cancellationToken)
    {
        using var client = await _listener.AcceptTcpClientAsync(cancellationToken);
        client.NoDelay = true;

        var playerId = _nextPlayerId++;
        var remoteAddress = client.Client.RemoteEndPoint is IPEndPoint remote
            ? remote.Address.ToString()
            : string.Empty;
        var session = new ProductionSocketSessionContext(playerId, remoteAddress);
        var receiveBuffer = new ProductionSocketReceiveBuffer();
        await using var stream = client.GetStream();
        var readBuffer = new byte[0x8000];

        while (!cancellationToken.IsCancellationRequested)
        {
            var read = await stream.ReadAsync(readBuffer, cancellationToken);
            if (read == 0)
                return new ProductionTcpSessionResult(playerId, ProductionTcpSessionStopReason.ClientDisconnected);

            receiveBuffer.Append(readBuffer.AsSpan(0, read));
            foreach (var frame in receiveBuffer.DrainFrames())
            {
                var result = await _handler.HandleFrameAsync(session, frame, cancellationToken);
                if (result.OutboundBytes.Length != 0)
                    await stream.WriteAsync(result.OutboundBytes, cancellationToken);

                if (!result.ContinueSession)
                    return new ProductionTcpSessionResult(playerId, ProductionTcpSessionStopReason.HandlerStopped);
            }
        }

        return new ProductionTcpSessionResult(playerId, ProductionTcpSessionStopReason.ClientDisconnected);
    }

    public void Dispose() => _listener.Stop();
}

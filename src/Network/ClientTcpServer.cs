using System.Net;
using System.Net.Sockets;

namespace GServ.Network;

public sealed record ClientSocketSessionContext(ushort PlayerId, string RemoteAddress);

public sealed record ClientSocketFrameResult(bool ContinueSession, byte[] OutboundBytes)
{
    public static ClientSocketFrameResult Continue(byte[]? outboundBytes = null) =>
        new(true, outboundBytes ?? []);

    public static ClientSocketFrameResult Stop(byte[]? outboundBytes = null) =>
        new(false, outboundBytes ?? []);
}

public interface IClientSocketFrameHandler
{
    ValueTask<ClientSocketFrameResult> HandleFrameAsync(
        ClientSocketSessionContext session,
        ReadOnlyMemory<byte> frame,
        CancellationToken cancellationToken);
}

public enum ClientTcpSessionStopReason
{
    ClientDisconnected,
    HandlerStopped
}

public sealed record ClientTcpSessionResult(
    ushort PlayerId,
    ClientTcpSessionStopReason StopReason);

public sealed class ClientTcpServer : IDisposable
{
    private const ushort PlayerIdInitialValue = 2;

    private readonly TcpListener _listener;
    private readonly IClientSocketFrameHandler _handler;
    private readonly TcpClientConnectionRegistry? _connectionRegistry;
    private ushort _nextPlayerId = PlayerIdInitialValue;

    public ClientTcpServer(
        IPAddress address,
        int port,
        IClientSocketFrameHandler handler,
        TcpClientConnectionRegistry? connectionRegistry = null)
    {
        _listener = new TcpListener(address, port);
        _handler = handler;
        _connectionRegistry = connectionRegistry;
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public void Start() => _listener.Start();

    public async Task<ClientTcpSessionResult> AcceptOneAsync(CancellationToken cancellationToken)
    {
        using var client = await _listener.AcceptTcpClientAsync(cancellationToken);
        client.NoDelay = true;

        var playerId = _nextPlayerId++;
        var remoteAddress = client.Client.RemoteEndPoint is IPEndPoint remote
            ? remote.Address.ToString()
            : string.Empty;
        var session = new ClientSocketSessionContext(playerId, remoteAddress);
        var receiveBuffer = new SocketReceiveBuffer();
        await using var stream = client.GetStream();
        _connectionRegistry?.Register(playerId, stream);
        var readBuffer = new byte[0x8000];

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var read = await stream.ReadAsync(readBuffer, cancellationToken);
                if (read == 0)
                    return new ClientTcpSessionResult(playerId, ClientTcpSessionStopReason.ClientDisconnected);

                receiveBuffer.Append(readBuffer.AsSpan(0, read));
                foreach (var frame in receiveBuffer.DrainFrames())
                {
                    var result = await _handler.HandleFrameAsync(session, frame, cancellationToken);
                    if (result.OutboundBytes.Length != 0)
                        await stream.WriteAsync(result.OutboundBytes, cancellationToken);

                    if (!result.ContinueSession)
                        return new ClientTcpSessionResult(playerId, ClientTcpSessionStopReason.HandlerStopped);
                }
            }

            return new ClientTcpSessionResult(playerId, ClientTcpSessionStopReason.ClientDisconnected);
        }
        finally
        {
            _connectionRegistry?.Unregister(playerId);
        }
    }

    public void Dispose() => _listener.Stop();
}

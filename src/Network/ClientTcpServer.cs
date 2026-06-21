using System.Net;
using System.Net.Sockets;

namespace Preagonal.GServer.Network;

public sealed record ClientSocketSessionContext(ushort PlayerId, string RemoteAddress);

public sealed record ClientSocketFrameResult(bool ContinueSession, byte[] OutboundBytes, string Diagnostic = "")
{
    public static ClientSocketFrameResult Continue(byte[]? outboundBytes = null, string diagnostic = "") =>
        new(true, outboundBytes ?? [], diagnostic);

    public static ClientSocketFrameResult Stop(byte[]? outboundBytes = null, string diagnostic = "") =>
        new(false, outboundBytes ?? [], diagnostic);
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
    HandlerStopped,
    Crashed
}

public sealed record ClientTcpSessionResult(
    ushort PlayerId,
    ClientTcpSessionStopReason StopReason,
    string Diagnostic = "");

public sealed class ClientTcpServer : IDisposable
{
    private const ushort PlayerIdInitialValue = 2;

    private readonly TcpListener _listener;
    private readonly IClientSocketFrameHandler _handler;
    private readonly TcpClientConnectionRegistry? _connectionRegistry;
    private readonly Action<ClientSocketSessionContext>? _accepted;
    private ushort _nextPlayerId = PlayerIdInitialValue;

    public ClientTcpServer(
        IPAddress address,
        int port,
        IClientSocketFrameHandler handler,
        TcpClientConnectionRegistry? connectionRegistry = null,
        Action<ClientSocketSessionContext>? accepted = null)
    {
        _listener = CreateListener(address, port);
        _handler = handler;
        _connectionRegistry = connectionRegistry;
        _accepted = accepted;
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public void Start() => _listener.Start();

    public async Task<ClientTcpSessionResult> AcceptOneAsync(CancellationToken cancellationToken)
    {
        using var client = await _listener.AcceptTcpClientAsync(cancellationToken);
        var session = CreateSession(client, _nextPlayerId++);
        _accepted?.Invoke(session);
        return await RunSessionAsync(client, session, cancellationToken);
    }

    public async Task RunAsync(CancellationToken cancellationToken, Action<ClientTcpSessionResult>? onSessionEnded = null)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                Console.WriteLine($"Client accept loop error: {ex.GetType().Name}: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                continue;
            }

            var session = CreateSession(client, _nextPlayerId++);
            _accepted?.Invoke(session);
            _ = RunSessionDisposingAsync(client, session, cancellationToken, onSessionEnded);
        }
    }

    private async Task RunSessionDisposingAsync(
        TcpClient client,
        ClientSocketSessionContext session,
        CancellationToken cancellationToken,
        Action<ClientTcpSessionResult>? onSessionEnded)
    {
        using (client)
        {
            try
            {
                var result = await RunSessionAsync(client, session, cancellationToken);
                onSessionEnded?.Invoke(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Client session {session.PlayerId} crashed: {ex.GetType().Name}: {ex.Message}");
                onSessionEnded?.Invoke(new ClientTcpSessionResult(
                    session.PlayerId,
                    ClientTcpSessionStopReason.Crashed,
                    $"{ex.GetType().Name}: {ex.Message}"));
            }
        }
    }

    private async Task<ClientTcpSessionResult> RunSessionAsync(
        TcpClient client,
        ClientSocketSessionContext session,
        CancellationToken cancellationToken)
    {
        client.NoDelay = true;
        var receiveBuffer = new SocketReceiveBuffer();
        await using var stream = client.GetStream();
        _connectionRegistry?.Register(session.PlayerId, stream);
        var readBuffer = new byte[0x8000];
        var lastDiagnostic = "";
        var firstReadDebug = "";
        var bytesRead = 0;
        var framesHandled = 0;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var read = await stream.ReadAsync(readBuffer, cancellationToken);
                if (read == 0)
                {
                    lastDiagnostic = AppendDisconnectDiagnostic(lastDiagnostic, bytesRead, framesHandled, receiveBuffer);
                    return new ClientTcpSessionResult(session.PlayerId, ClientTcpSessionStopReason.ClientDisconnected, lastDiagnostic);
                }

                bytesRead += read;
                if (firstReadDebug.Length == 0)
                    firstReadDebug = BuildReadDebug(readBuffer.AsSpan(0, read));

                receiveBuffer.Append(readBuffer.AsSpan(0, read));
                foreach (var frame in receiveBuffer.DrainFrames())
                {
                    framesHandled++;
                    var result = await _handler.HandleFrameAsync(session, frame, cancellationToken);
                    if (!string.IsNullOrEmpty(result.Diagnostic))
                        lastDiagnostic = result.Diagnostic;

                    if (result.OutboundBytes.Length != 0)
                        await stream.WriteAsync(result.OutboundBytes, cancellationToken);

                    if (!result.ContinueSession)
                    {
                        lastDiagnostic = AppendReadDebug(lastDiagnostic, firstReadDebug);
                        return new ClientTcpSessionResult(session.PlayerId, ClientTcpSessionStopReason.HandlerStopped, lastDiagnostic);
                    }
                }
            }

            return new ClientTcpSessionResult(session.PlayerId, ClientTcpSessionStopReason.ClientDisconnected, lastDiagnostic);
        }
        finally
        {
            _connectionRegistry?.Unregister(session.PlayerId);
        }
    }

    public void Dispose() => _listener.Stop();

    private static TcpListener CreateListener(IPAddress address, int port) =>
        new(address, port);

    private static ClientSocketSessionContext CreateSession(TcpClient client, ushort playerId)
    {
        var remoteAddress = client.Client.RemoteEndPoint is IPEndPoint remote
            ? remote.Address.ToString()
            : string.Empty;

        return new ClientSocketSessionContext(playerId, remoteAddress);
    }

    private static string BuildReadDebug(ReadOnlySpan<byte> bytes)
    {
        var previewLength = Math.Min(bytes.Length, 96);
        return $"firstReadHex={Convert.ToHexString(bytes[..previewLength])}; firstReadBytes={bytes.Length}";
    }

    private static string AppendReadDebug(string diagnostic, string readDebug)
    {
        if (string.IsNullOrEmpty(readDebug))
            return diagnostic;

        return string.IsNullOrEmpty(diagnostic)
            ? readDebug
            : $"{diagnostic}; {readDebug}";
    }

    private static string AppendDisconnectDiagnostic(
        string diagnostic,
        int bytesRead,
        int framesHandled,
        SocketReceiveBuffer receiveBuffer)
    {
        var pendingLength = receiveBuffer.PendingFrameLength is { } length
            ? length.ToString()
            : "none";
        var transport = $"transport bytes={bytesRead}; frames={framesHandled}; buffered={receiveBuffer.BufferedByteCount}; pendingLength={pendingLength}";

        return string.IsNullOrEmpty(diagnostic)
            ? transport
            : $"{diagnostic}; {transport}";
    }
}

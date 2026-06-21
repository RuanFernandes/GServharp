namespace Preagonal.GServer.Network;

public sealed class LoginSocketFrameHandler(
    LoginAuthBridge bridge,
    TcpClientConnectionRegistry? connections = null) : IClientSocketFrameHandler
{
    public async ValueTask<ClientSocketFrameResult> HandleFrameAsync(
        ClientSocketSessionContext session,
        ReadOnlyMemory<byte> frame,
        CancellationToken cancellationToken)
    {
        var result = bridge.HandleClientFrame(session, frame.Span);
        if (!string.IsNullOrEmpty(result.Diagnostic) &&
            (result.Diagnostic.StartsWith("active packets=", StringComparison.Ordinal) ||
             result.Diagnostic.StartsWith("frame=", StringComparison.Ordinal)))
            Console.WriteLine($"Client session {session.PlayerId}: {result.Diagnostic}");
        if (connections is not null)
        {
            foreach (var broadcast in result.Broadcasts)
            {
                if (broadcast.OutboundBytes.Length != 0)
                    await connections.SendAsync(broadcast.PlayerId, broadcast.OutboundBytes, cancellationToken);
            }
        }

        return result.ContinueSession
            ? ClientSocketFrameResult.Continue(result.OutboundBytes, result.Diagnostic)
            : ClientSocketFrameResult.Stop(result.OutboundBytes, result.Diagnostic);
    }
}

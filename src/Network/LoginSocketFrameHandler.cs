namespace GServ.Network;

public sealed class LoginSocketFrameHandler(LoginAuthBridge bridge) : IClientSocketFrameHandler
{
    public ValueTask<ClientSocketFrameResult> HandleFrameAsync(
        ClientSocketSessionContext session,
        ReadOnlyMemory<byte> frame,
        CancellationToken cancellationToken)
    {
        var result = bridge.BeginClientLogin(session, frame.Span);
        return ValueTask.FromResult(result.Accepted
            ? ClientSocketFrameResult.Continue(result.OutboundBytes)
            : ClientSocketFrameResult.Stop(result.OutboundBytes));
    }
}

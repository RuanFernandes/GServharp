namespace GServ.Network;

public sealed class ProductionLoginSocketFrameHandler(ProductionLoginAuthBridge bridge) : IProductionSocketFrameHandler
{
    public ValueTask<ProductionSocketFrameResult> HandleFrameAsync(
        ProductionSocketSessionContext session,
        ReadOnlyMemory<byte> frame,
        CancellationToken cancellationToken)
    {
        var result = bridge.BeginClientLogin(session, frame.Span);
        return ValueTask.FromResult(result.Accepted
            ? ProductionSocketFrameResult.Continue(result.OutboundBytes)
            : ProductionSocketFrameResult.Stop(result.OutboundBytes));
    }
}

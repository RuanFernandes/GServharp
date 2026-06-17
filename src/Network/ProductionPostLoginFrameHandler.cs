using GServ.Game;
using GServ.Protocol;

namespace GServ.Network;

public sealed class ProductionPostLoginFrameHandler : IProductionSocketFrameHandler
{
    private readonly InboundPacketDecoder _decoder;
    private readonly ClientPacketStreamFramer _framer = new(new ClientPacketParseOptions(StripRawDataTrailingNewline: true));
    private readonly ProductionPostLoginPacketDispatcher _dispatcher;
    private readonly Action<string> _log;

    public ProductionPostLoginFrameHandler(
        RuntimePlayer player,
        EncryptionGeneration inboundGeneration,
        byte key,
        Action<string>? log = null)
    {
        _decoder = new InboundPacketDecoder(inboundGeneration, key);
        _dispatcher = new ProductionPostLoginPacketDispatcher(player);
        _log = log ?? (_ => { });
    }

    public ValueTask<ProductionSocketFrameResult> HandleFrameAsync(
        ProductionSocketSessionContext session,
        ReadOnlyMemory<byte> frame,
        CancellationToken cancellationToken)
    {
        var decoded = _decoder.DecodeSocketFrame(frame.Span);
        foreach (var warning in decoded.Warnings)
            _log(warning);

        var packets = _framer.Parse(decoded.DecodedPayload);
        foreach (var packet in packets)
        {
            var result = _dispatcher.DispatchDecodedPacket(packet.Payload.Span);
            _log($"Session {session.PlayerId}: {result.Status}: {result.Message}");

            if (result.ContinueSession)
                continue;

            return ValueTask.FromResult(new ProductionSocketFrameResult(false, result.OutboundBytes));
        }

        return ValueTask.FromResult(ProductionSocketFrameResult.Continue());
    }
}

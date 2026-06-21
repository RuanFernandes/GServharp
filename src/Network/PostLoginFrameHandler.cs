using Preagonal.GServer.Game;
using Preagonal.GServer.Protocol;

namespace Preagonal.GServer.Network;

public sealed class PostLoginFrameHandler : IClientSocketFrameHandler
{
    private readonly InboundPacketDecoder _decoder;
    private readonly ClientPacketStreamFramer _framer = new(new ClientPacketParseOptions(StripRawDataTrailingNewline: true));
    private readonly PostLoginPacketDispatcher _dispatcher;
    private readonly Action<string> _log;

    public PostLoginFrameHandler(
        RuntimePlayer player,
        EncryptionGeneration inboundGeneration,
        byte key,
        Action<string>? log = null)
    {
        _decoder = new InboundPacketDecoder(inboundGeneration, key);
        _dispatcher = new PostLoginPacketDispatcher(player);
        _log = log ?? (_ => { });
    }

    public ValueTask<ClientSocketFrameResult> HandleFrameAsync(
        ClientSocketSessionContext session,
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

            return ValueTask.FromResult(new ClientSocketFrameResult(false, result.OutboundBytes));
        }

        return ValueTask.FromResult(ClientSocketFrameResult.Continue());
    }
}

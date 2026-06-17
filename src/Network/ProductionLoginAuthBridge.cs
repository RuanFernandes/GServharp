using GServ.Protocol;

namespace GServ.Network;

public sealed record ProductionClientLoginAuthResult(
    bool Accepted,
    SessionLifecycle Lifecycle,
    byte[] OutboundBytes);

public sealed record ProductionServerListLoginResponseResult(
    ProductionServerListAuthResponseStatus Status,
    ushort PlayerId,
    PlayerSessionType Type,
    byte[] OutboundBytes);

public sealed class ProductionLoginAuthBridge(
    IProductionServerListGateway serverList,
    PreWorldAuthOptions options)
{
    private readonly Dictionary<(ushort PlayerId, PlayerSessionType Type), ClientSessionSkeleton> _pendingSessions = [];

    public ProductionClientLoginAuthResult BeginClientLogin(
        ProductionSocketSessionContext context,
        ReadOnlySpan<byte> loginFrame)
    {
        var session = new ClientSessionSkeleton(context.PlayerId);
        if (!session.ReceiveLoginPacket(loginFrame))
            return Finish(session, accepted: false);

        var auth = new ProductionAuthServerListBoundary(serverList, options);
        var result = auth.Begin(session);
        if (!result.Accepted)
            return Finish(session, accepted: false);

        _pendingSessions[(session.Id, session.Type)] = session;
        return Finish(session, accepted: true);
    }

    public ProductionServerListLoginResponseResult HandleVerifyAccount2(ReadOnlySpan<byte> payloadWithoutPacketId)
    {
        var handler = new ProductionServerListAuthResponseHandler(FindSession);
        var result = handler.HandleVerifyAccount2(payloadWithoutPacketId);
        var response = result.Response;
        var key = (response.PlayerId, response.Type);
        var session = FindSession(response.PlayerId, response.Type);
        var outbound = session?.TakeOutboundBytes() ?? [];

        if (result.Status != ProductionServerListAuthResponseStatus.AcceptedPreWorld)
            _pendingSessions.Remove(key);

        return new ProductionServerListLoginResponseResult(
            result.Status,
            response.PlayerId,
            response.Type,
            outbound);
    }

    private ClientSessionSkeleton? FindSession(ushort id, PlayerSessionType type) =>
        _pendingSessions.TryGetValue((id, type), out var session) ? session : null;

    private static ProductionClientLoginAuthResult Finish(ClientSessionSkeleton session, bool accepted) =>
        new(accepted, session.Lifecycle, session.TakeOutboundBytes());
}

using GServ.Protocol;

namespace GServ.Network;

public enum ProductionServerListAuthResponseStatus
{
    SessionNotFound,
    AcceptedPreWorld,
    Rejected
}

public sealed record ProductionServerListAuthResponseResult(
    ProductionServerListAuthResponseStatus Status,
    ServerListVerifyAccount2Response Response)
{
    public bool SessionFound => Status != ProductionServerListAuthResponseStatus.SessionNotFound;
}

public sealed class ProductionServerListAuthResponseHandler(
    Func<ushort, PlayerSessionType, ClientSessionSkeleton?> findSession)
{
    public ProductionServerListAuthResponseResult HandleVerifyAccount2(ReadOnlySpan<byte> payloadWithoutPacketId)
    {
        var response = ServerListAuthPackets.ParseVerifyAccount2Response(payloadWithoutPacketId);
        var session = findSession(response.PlayerId, response.Type);

        if (session is null)
            return new ProductionServerListAuthResponseResult(
                ProductionServerListAuthResponseStatus.SessionNotFound,
                response);

        var accepted = session.ReceiveServerListAuthResponse(response);
        return new ProductionServerListAuthResponseResult(
            accepted
                ? ProductionServerListAuthResponseStatus.AcceptedPreWorld
                : ProductionServerListAuthResponseStatus.Rejected,
            response);
    }
}

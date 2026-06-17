using GServ.Protocol;

namespace GServ.Network;

public interface IServerListGateway
{
    bool IsConnected { get; }
    void SendLoginPacketForPlayer(byte[] packetBody);
}

public sealed class ServerListAuthBoundary(
    IServerListGateway serverList,
    PreWorldAuthOptions options)
{
    public PreWorldAuthResult Begin(ClientSessionSkeleton session)
    {
        var effectiveOptions = options with { IsServerListConnected = serverList.IsConnected };
        var result = new PreWorldAuthBoundary(effectiveOptions).Begin(session);
        if (result.Accepted)
            serverList.SendLoginPacketForPlayer(result.ServerListRequest);

        return result;
    }
}

using Preagonal.GServer.Protocol;

namespace Preagonal.GServer.Network;

public sealed record PreWorldAuthOptions(
    int MaxPlayers,
    int CurrentPlayerCount,
    bool IsIpBanned,
    bool IsServerListConnected,
    IReadOnlyList<string> AllowedVersions,
    string AllowedVersionText);

public sealed record PreWorldAuthResult(bool Accepted, byte[] ServerListRequest);

public sealed class PreWorldAuthBoundary
{
    private readonly PreWorldAuthOptions _options;

    public PreWorldAuthBoundary(PreWorldAuthOptions options)
    {
        _options = options;
    }

    public PreWorldAuthResult Begin(ClientSessionSkeleton session)
    {
        if (session.LoginPacket is null)
            throw new InvalidOperationException("Login packet must be parsed before pre-world authentication.");

        if (_options.CurrentPlayerCount >= _options.MaxPlayers)
            return Reject(session, "This server has reached its player limit.");

        if (_options.IsIpBanned)
            return Reject(session, "You have been banned from this server.");

        if (IsClient(session.Type) &&
            !AllowedVersionPolicy.IsAllowed(session.LoginPacket.VersionId, _options.AllowedVersions))
        {
            return Reject(session, $"Your client version is not allowed on this server.\rAllowed: {_options.AllowedVersionText}");
        }

        if (!_options.IsServerListConnected)
            return Reject(session, "The login server is offline.  Try again later.");

        session.MarkWaitingForServerListAuth();
        var request = ServerListAuthPackets.VerifyAccount2Request(
            session.LoginPacket.AccountName,
            session.LoginPacket.Password,
            session.Id,
            session.Type,
            session.LoginPacket.Identity);
        return new PreWorldAuthResult(true, request);
    }

    private static PreWorldAuthResult Reject(ClientSessionSkeleton session, string message)
    {
        session.QueueDisconnect(message);
        return new PreWorldAuthResult(false, []);
    }

    private static bool IsClient(PlayerSessionType type) =>
        (type & PlayerSessionType.AnyClient) != 0;
}

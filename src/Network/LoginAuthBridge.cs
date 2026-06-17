using Preagonal.GServer.Protocol;

namespace Preagonal.GServer.Network;

public sealed record ClientLoginAuthResult(
    bool Accepted,
    SessionLifecycle Lifecycle,
    byte[] OutboundBytes,
    string Diagnostic = "");

public sealed record ServerListLoginResponseResult(
    ServerListAuthResponseStatus Status,
    ushort PlayerId,
    PlayerSessionType Type,
    byte[] OutboundBytes,
    IReadOnlyList<ClientSessionOutbound> Broadcasts);

public sealed record ClientSessionOutbound(ushort PlayerId, byte[] OutboundBytes);

public sealed class LoginAuthBridge(
    IServerListGateway serverList,
    PreWorldAuthOptions options,
    LoginWorldEntryOptions? worldEntryOptions = null)
{
    private readonly Dictionary<(ushort PlayerId, PlayerSessionType Type), ClientSessionSkeleton> _pendingSessions = [];
    private readonly Dictionary<(ushort PlayerId, PlayerSessionType Type), string> _remoteAddresses = [];
    private readonly Dictionary<ushort, ClientSessionSkeleton> _activeSessions = [];
    private readonly Dictionary<ushort, PostLoginPlayerSnapshot> _activeSnapshots = [];
    private readonly Dictionary<ushort, string> _loginFrameDebug = [];

    public ClientLoginAuthResult BeginClientLogin(
        ClientSocketSessionContext context,
        ReadOnlySpan<byte> loginFrame)
    {
        if (HasPendingSession(context.PlayerId))
            return new ClientLoginAuthResult(
                Accepted: true,
                Lifecycle: SessionLifecycle.WaitingForServerListAuth,
                OutboundBytes: [],
                Diagnostic: $"login frame ignored while auth pending; {BuildFrameDebug(loginFrame)}");

        var session = new ClientSessionSkeleton(context.PlayerId);
        _loginFrameDebug[context.PlayerId] = BuildFrameDebug(loginFrame);
        if (!session.ReceiveLoginPacket(loginFrame))
            return Finish(session, accepted: false);

        var auth = new ServerListAuthBoundary(serverList, options);
        var result = auth.Begin(session);
        if (!result.Accepted)
            return Finish(session, accepted: false);

        var key = (session.Id, session.Type);
        _pendingSessions[key] = session;
        _remoteAddresses[key] = context.RemoteAddress;
        return Finish(session, accepted: true);
    }

    public ServerListLoginResponseResult HandleVerifyAccount2(ReadOnlySpan<byte> payloadWithoutPacketId)
    {
        var handler = new ServerListAuthResponseHandler(FindSession);
        var result = handler.HandleVerifyAccount2(payloadWithoutPacketId);
        var response = result.Response;
        var key = (response.PlayerId, response.Type);
        var session = FindSession(response.PlayerId, response.Type);
        var broadcasts = Array.Empty<ClientSessionOutbound>();
        if (result.Status == ServerListAuthResponseStatus.AcceptedPreWorld &&
            session is not null &&
            worldEntryOptions is not null &&
            LoginWorldEntry.Complete(session, worldEntryOptions with
            {
                AccountLoginOptions = worldEntryOptions.AccountLoginOptions with
                {
                    RemoteIp = _remoteAddresses.GetValueOrDefault(key, worldEntryOptions.AccountLoginOptions.RemoteIp)
                }
            }, out var playerAdd, out var snapshot))
        {
            broadcasts = ExchangeLoginPlayerProps(session, snapshot).ToArray();
            _activeSessions[session.Id] = session;
            _activeSnapshots[session.Id] = snapshot;
            serverList.SendPlayerAdd(playerAdd);
        }

        var outbound = session is null ? [] : FlushOutboundBytes(session);

        if (result.Status != ServerListAuthResponseStatus.AcceptedPreWorld)
        {
            _pendingSessions.Remove(key);
            _remoteAddresses.Remove(key);
        }

        return new ServerListLoginResponseResult(
            result.Status,
            response.PlayerId,
            response.Type,
            outbound,
            broadcasts);
    }

    private ClientSessionSkeleton? FindSession(ushort id, PlayerSessionType type) =>
        _pendingSessions.TryGetValue((id, type), out var session) ? session : null;

    private bool HasPendingSession(ushort id) =>
        _pendingSessions.Keys.Any(key => key.PlayerId == id);

    private IEnumerable<ClientSessionOutbound> ExchangeLoginPlayerProps(
        ClientSessionSkeleton joiningSession,
        PostLoginPlayerSnapshot joiningSnapshot)
    {
        if (!IsClient(joiningSession.Type))
            yield break;

        foreach (var (otherId, otherSnapshot) in _activeSnapshots.ToArray())
        {
            if (otherId == joiningSession.Id || !IsClient(otherSnapshot.Type))
                continue;

            joiningSession.QueuePacket(BuildOtherPlayerProps(otherSnapshot));

            if (!_activeSessions.TryGetValue(otherId, out var otherSession))
                continue;

            otherSession.QueuePacket(BuildOtherPlayerProps(joiningSnapshot));
            var outbound = FlushOutboundBytes(otherSession);
            if (outbound.Length != 0)
                yield return new ClientSessionOutbound(otherId, outbound);
        }
    }

    private static byte[] BuildOtherPlayerProps(PostLoginPlayerSnapshot snapshot)
    {
        var payload = PlayerPropertySerializer.SerializeOtherPlayerPropsPayload(
            snapshot.LoginPropertySource,
            GetLoginPropertySet.All);
        return PlayerPropertySerializer.BuildOtherPlayerPropsPacket(snapshot.PlayerId, payload, appendNewline: true);
    }

    private static bool IsClient(PlayerSessionType type) =>
        (type & PlayerSessionType.AnyClient) != 0;

    private ClientLoginAuthResult Finish(ClientSessionSkeleton session, bool accepted) =>
        new(accepted, session.Lifecycle, FlushOutboundBytes(session), BuildDiagnostic(session, accepted));

    private string BuildDiagnostic(ClientSessionSkeleton session, bool accepted)
    {
        if (session.LoginPacket is null)
            return $"login accepted={accepted}; lifecycle={session.Lifecycle}; login packet missing; {_loginFrameDebug.GetValueOrDefault(session.Id, "")}";

        return $"login accepted={accepted}; lifecycle={session.Lifecycle}; type={session.Type}; account={session.LoginPacket.AccountName}; version={session.LoginPacket.VersionToken}; versionId={session.LoginPacket.VersionId}; identity={session.LoginPacket.Identity}; {_loginFrameDebug.GetValueOrDefault(session.Id, "")}; {session.LoginPacket.DebugInfo}";
    }

    private static string BuildFrameDebug(ReadOnlySpan<byte> frame)
    {
        var previewLength = Math.Min(frame.Length, 96);
        return $"frameHex={Convert.ToHexString(frame[..previewLength])}; frameBytes={frame.Length}";
    }

    private static byte[] FlushOutboundBytes(ClientSessionSkeleton session)
    {
        var raw = session.TakeOutboundBytes();
        if (raw.Length == 0)
            return [];

        var queue = new GraalFileQueue();
        if (session.LoginPacket?.Type is PlayerSessionType.Client3 or PlayerSessionType.RemoteControl2 &&
            session.LoginPacket.EncryptionKey is { } key)
        {
            queue.SetCodec(EncryptionGeneration.Gen5, key);
        }
        else if (session.LoginPacket?.Type == PlayerSessionType.Web)
        {
            queue.SetCodec(EncryptionGeneration.Gen1, key: 0);
        }

        queue.AddPacket(raw);
        return queue.FlushSocket(forceSendFiles: true);
    }
}

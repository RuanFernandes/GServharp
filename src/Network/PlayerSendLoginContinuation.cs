using Preagonal.GServer.Protocol;

namespace Preagonal.GServer.Network;

public sealed record PlayerSendLoginAccount(
    string AccountName,
    bool IsBanned,
    string BanReason,
    bool HasModifyStaffAccountRight,
    bool IsStaff,
    bool IsAdminIp,
    IReadOnlyList<string> AdminIps,
    bool IsGuest);

public sealed record ActivePlayerSession(
    ushort Id,
    string AccountName,
    PlayerSessionType Type,
    TimeSpan AgeSinceLastData);

public sealed record DuplicateSessionDisconnect(ushort SessionId, string Message);

public sealed record PlayerSendLoginOptions(
    bool OnlyStaff,
    string ServerName,
    IReadOnlyList<ActivePlayerSession> ActiveSessions);

public sealed record PlayerSendLoginContinuationResult(
    bool Accepted,
    IReadOnlyList<DuplicateSessionDisconnect> DuplicateDisconnects,
    bool LoginServerFullStopBlocked = false);

public static class PlayerSendLoginContinuation
{
    private const string DuplicateSessionDisconnectMessage = "Someone else has logged into your account.";

    public static PlayerSendLoginContinuationResult Begin(
        ClientSessionSkeleton session,
        PlayerSendLoginAccount account,
        PlayerSendLoginOptions options)
    {
        if (session.LoginPacket is null)
            throw new InvalidOperationException("Login packet must be parsed before sendLogin continuation.");

        if (session.Lifecycle != SessionLifecycle.ServerListAuthAcceptedPreWorld)
            throw new InvalidOperationException("Server-list authentication must succeed before sendLogin continuation.");

        if (account.IsBanned && !account.HasModifyStaffAccountRight)
            return Reject(session, $"You have been banned.  Reason: {FormatBanReason(account.BanReason)}");

        if (IsRemoteControlOrNpcControl(session.Type) && (!account.IsStaff || !account.IsAdminIp))
            return Reject(session, "You do not have RC rights.");

        if (IsClient(session.Type))
        {
            if (options.OnlyStaff && !account.IsStaff)
                return Reject(session, "This server is currently restricted to staff only.");

            if (!account.IsAdminIp && !account.AdminIps.Contains("0.0.0.0", StringComparer.Ordinal))
                return Reject(session, "Your IP doesn't match one of the allowed IPs for this account.");
        }

        session.QueuePacket(OutboundLoginPackets.Signature(appendNewline: true));

        var loginServerFullStopBlocked = options.ServerName.Contains("login", StringComparison.OrdinalIgnoreCase);
        if (loginServerFullStopBlocked)
        {
            // C++ sends PLO_FULLSTOP here, but recovered gs2lib does not define PLO_FULLSTOP.
            // The branch is documented as blocked until the original opcode source is recovered.
        }

        if (IsClient(session.Type))
            session.QueuePacket(OutboundLoginPackets.Unknown168(appendNewline: true));

        if (!account.IsGuest)
        {
            var duplicateDisconnects = new List<DuplicateSessionDisconnect>();
            foreach (var activeSession in options.ActiveSessions)
            {
                if (!IsSameDuplicateFamily(session, account.AccountName, activeSession))
                    continue;

                duplicateDisconnects.Add(new DuplicateSessionDisconnect(
                    activeSession.Id,
                    DuplicateSessionDisconnectMessage));
            }

            session.MarkReadyForWorldEntry();
            return new PlayerSendLoginContinuationResult(true, duplicateDisconnects, loginServerFullStopBlocked);
        }

        session.MarkReadyForWorldEntry();
        return new PlayerSendLoginContinuationResult(true, [], loginServerFullStopBlocked);
    }

    private static PlayerSendLoginContinuationResult Reject(ClientSessionSkeleton session, string message)
    {
        session.QueueDisconnect(message);
        return new PlayerSendLoginContinuationResult(false, []);
    }

    private static bool IsSameDuplicateFamily(
        ClientSessionSkeleton session,
        string accountName,
        ActivePlayerSession activeSession)
    {
        if (session.Id == activeSession.Id)
            return false;

        if (!string.Equals(accountName, activeSession.AccountName, StringComparison.OrdinalIgnoreCase))
            return false;

        return GetClientFamily(session.Type) == GetClientFamily(activeSession.Type);
    }

    private static int GetClientFamily(PlayerSessionType type)
    {
        if (IsClient(type))
            return 0;
        if (IsRemoteControl(type))
            return 1;
        return 2;
    }

    private static string FormatBanReason(string banReason) =>
        banReason.Replace('\n', '\r');

    private static bool IsClient(PlayerSessionType type) =>
        (type & PlayerSessionType.AnyClient) != 0;

    private static bool IsRemoteControl(PlayerSessionType type) =>
        (type & PlayerSessionType.AnyRemoteControl) != 0;

    private static bool IsRemoteControlOrNpcControl(PlayerSessionType type) =>
        (type & (PlayerSessionType.AnyRemoteControl | PlayerSessionType.AnyNpcControl)) != 0;
}

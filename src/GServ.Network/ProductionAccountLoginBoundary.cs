using GServ.Persistence;

namespace GServ.Network;

public sealed record ProductionAccountLoginOptions(
    bool OnlyStaff,
    string ServerName,
    IReadOnlyList<ActivePlayerSession> ActiveSessions,
    IReadOnlyList<string> StaffAccounts,
    string RemoteIp,
    IGuestIdentitySelector? GuestIdentitySelector = null);

public sealed record ProductionAccountLoginResult(
    bool Accepted,
    bool AccountLoaded,
    bool GuestIdentityBlocked,
    string? GuestAccountName,
    AccountSaveResult? CreatedAccountSave,
    IReadOnlyList<DuplicateSessionDisconnect> DuplicateDisconnects);

public static class ProductionAccountLoginBoundary
{
    private const int ModifyStaffAccountRight = 0x04000;

    public static ProductionAccountLoginResult Begin(
        ClientSessionSkeleton session,
        IAccountPersistenceFileSystem fileSystem,
        IAccountLoadSettings settings,
        ProductionAccountLoginOptions options,
        AccountParserOptions? parserOptions = null)
    {
        if (session.LoginPacket is null)
            throw new InvalidOperationException("Login packet must be parsed before account login.");

        if (session.Lifecycle != SessionLifecycle.ServerListAuthAcceptedPreWorld)
            throw new InvalidOperationException("Server-list authentication must succeed before account loading.");

        var ignoreNickname = IsRemoteControlOrNpcControl(session.Type);
        var accountName = session.LoginPacket.AccountName;
        var load = AccountLoadService.Load(accountName, fileSystem, settings, ignoreNickname, parserOptions);
        if (!load.Success)
        {
            session.QueueDisconnect("Unable to load account.");
            return new ProductionAccountLoginResult(false, false, false, null, null, []);
        }

        if (load.RequiresGuestIdentityGeneration)
        {
            if (options.GuestIdentitySelector is null)
                return new ProductionAccountLoginResult(false, true, true, null, null, []);

            var identity = options.GuestIdentitySelector.TrySelect(accountName =>
                options.ActiveSessions.Any(activeSession =>
                    string.Equals(activeSession.AccountName, accountName, StringComparison.OrdinalIgnoreCase)));
            if (!identity.Success || identity.AccountName is null)
                return new ProductionAccountLoginResult(false, true, true, null, null, []);

            load.Account!.AccountName = identity.AccountName;
            load.Account.CommunityName = "guest";
        }

        AccountSaveResult? createdAccountSave = null;
        if (load.ShouldSaveCreatedAccount)
            createdAccountSave = AccountSaveService.SaveCreatedDefaultAccount(load.Account!, fileSystem, accountName);

        var account = ToPlayerSendLoginAccount(
            load.Account!,
            options.StaffAccounts,
            options.RemoteIp,
            isGuest: load.RequiresGuestIdentityGeneration);

        var continuation = PlayerSendLoginContinuation.Begin(
            session,
            account,
            new PlayerSendLoginOptions(
                options.OnlyStaff,
                options.ServerName,
                options.ActiveSessions));

        return new ProductionAccountLoginResult(
            continuation.Accepted,
            true,
            false,
            load.RequiresGuestIdentityGeneration ? load.Account!.AccountName : null,
            createdAccountSave,
            continuation.DuplicateDisconnects);
    }

    public static PlayerSendLoginAccount ToPlayerSendLoginAccount(
        AccountFileData account,
        IReadOnlyList<string> staffAccounts,
        string remoteIp,
        bool isGuest)
    {
        var adminIps = SplitAdminIps(account.AdminIp);
        return new PlayerSendLoginAccount(
            account.AccountName,
            account.IsBanned,
            account.BanReason,
            (account.AdminRights & ModifyStaffAccountRight) != 0,
            IsStaff(account.AccountName, staffAccounts),
            adminIps.Any(mask => CppWildcardMatch(remoteIp, mask)),
            adminIps,
            isGuest);
    }

    private static bool IsRemoteControlOrNpcControl(Protocol.PlayerSessionType type) =>
        (type & (Protocol.PlayerSessionType.AnyRemoteControl | Protocol.PlayerSessionType.AnyNpcControl)) != 0;

    private static IReadOnlyList<string> SplitAdminIps(string adminIp)
    {
        if (adminIp.Length == 0)
            return Array.Empty<string>();

        return adminIp
            .Split(',', StringSplitOptions.None)
            .Select(ip => ip.Trim())
            .ToArray();
    }

    private static bool IsStaff(string accountName, IReadOnlyList<string> staffAccounts)
    {
        foreach (var staffAccount in staffAccounts)
        {
            if (string.Equals(accountName, staffAccount.Trim(), StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool CppWildcardMatch(string value, string mask)
    {
        var valueIndex = 0;
        var maskIndex = 0;
        char? stop = null;

        while (valueIndex < value.Length)
        {
            if (maskIndex < mask.Length && mask[maskIndex] == '*')
            {
                maskIndex++;
                if (maskIndex >= mask.Length)
                    return true;

                stop = mask[maskIndex];
            }

            if (stop is { } stopChar)
            {
                if (stopChar == value[valueIndex])
                {
                    maskIndex++;
                    valueIndex++;
                    stop = null;
                }
                else
                {
                    valueIndex++;
                }
            }
            else if (maskIndex < mask.Length &&
                (mask[maskIndex] == value[valueIndex] || mask[maskIndex] == '?'))
            {
                maskIndex++;
                valueIndex++;
            }
            else
            {
                return false;
            }

            if (valueIndex >= value.Length &&
                maskIndex < mask.Length &&
                mask[maskIndex] != '*')
            {
                return false;
            }
        }

        return true;
    }
}

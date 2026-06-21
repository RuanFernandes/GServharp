using Preagonal.GServer.Persistence;

namespace Preagonal.GServer.Network;

public sealed record AccountLoginOptions(
    bool OnlyStaff,
    string ServerName,
    IReadOnlyList<ActivePlayerSession> ActiveSessions,
    IReadOnlyList<string> StaffAccounts,
    string RemoteIp,
    IGuestIdentitySelector? GuestIdentitySelector = null);

public sealed record AccountLoginResult(
    bool Accepted,
    bool AccountLoaded,
    bool GuestIdentityBlocked,
    string? GuestAccountName,
    AccountSaveResult? CreatedAccountSave,
    IReadOnlyList<DuplicateSessionDisconnect> DuplicateDisconnects,
    AccountFileData? Account = null);

public static class AccountLoginBoundary
{
    private const int ModifyStaffAccountRight = 0x04000;

    public static AccountLoginResult Begin(
        ClientSessionSkeleton session,
        IAccountPersistenceFileSystem fileSystem,
        IAccountLoadSettings settings,
        AccountLoginOptions options,
        AccountParserOptions? parserOptions = null)
    {
        if (session.LoginPacket is null)
            throw new InvalidOperationException("Login packet must be parsed before account login.");

        if (session.Lifecycle != SessionLifecycle.ServerListAuthAcceptedPreWorld)
            throw new InvalidOperationException("Server-list authentication must succeed before account loading.");

        var accountName = NormalizeAccountName(session.LoginPacket.AccountName);
        var load = AccountLoadService.Load(accountName, fileSystem, settings, ignoreNickname: false, parserOptions);
        if (!load.Success)
        {
            session.QueueDisconnect("Unable to load account.");
            return new AccountLoginResult(false, false, false, null, null, []);
        }

        if (load.RequiresGuestIdentityGeneration)
        {
            if (options.GuestIdentitySelector is null)
                return new AccountLoginResult(false, true, true, null, null, [], load.Account);

            var identity = options.GuestIdentitySelector.TrySelect(accountName =>
                options.ActiveSessions.Any(activeSession =>
                    string.Equals(activeSession.AccountName, accountName, StringComparison.OrdinalIgnoreCase)));
            if (!identity.Success || identity.AccountName is null)
                return new AccountLoginResult(false, true, true, null, null, [], load.Account);

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

        return new AccountLoginResult(
            continuation.Accepted,
            true,
            false,
            load.RequiresGuestIdentityGeneration ? load.Account!.AccountName : null,
            createdAccountSave,
            continuation.DuplicateDisconnects,
            load.Account);
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

    private static string NormalizeAccountName(string accountName) =>
        accountName.StartsWith("pc:", StringComparison.OrdinalIgnoreCase)
            ? accountName[3..]
            : accountName;

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

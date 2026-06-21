using System.Globalization;

namespace Preagonal.GServer.Persistence;

public interface IAccountFileSystem
{
    string ServerPath { get; }
    string? FindCaseInsensitive(string fileName);
    string? ReadAllText(string path);
}

public interface IAccountLoadSettings
{
    bool Exists(string key);
    string GetString(string key, string defaultValue);
    float GetFloat(string key, float defaultValue);
}

public sealed class AccountLoadSettings(IReadOnlyDictionary<string, string> values) : IAccountLoadSettings
{
    public static AccountLoadSettings Empty { get; } = new(new Dictionary<string, string>());

    public bool Exists(string key) =>
        values.ContainsKey(key);

    public string GetString(string key, string defaultValue) =>
        values.TryGetValue(key, out var value) ? value : defaultValue;

    public float GetFloat(string key, float defaultValue) =>
        values.TryGetValue(key, out var value) &&
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : defaultValue;
}

public sealed record AccountLoadResult(
    bool Success,
    AccountFileData? Account,
    string? SourcePath,
    bool LoadedFromDefault,
    bool ShouldSaveCreatedAccount,
    string? AccountFileToAdd,
    bool RequiresGuestIdentityGeneration)
{
    public static AccountLoadResult Invalid(string? sourcePath, bool loadedFromDefault) =>
        new(false, null, sourcePath, loadedFromDefault, false, null, false);
}

public static class AccountLoadService
{
    public static AccountLoadResult Load(
        string accountName,
        IAccountFileSystem fileSystem,
        IAccountLoadSettings settings,
        bool ignoreNickname = false,
        AccountParserOptions? parserOptions = null)
    {
        var loadedFromDefault = false;
        var sourcePath = fileSystem.FindCaseInsensitive(accountName + ".txt");

        if (string.IsNullOrEmpty(sourcePath))
        {
            sourcePath = BuildDefaultAccountPath(fileSystem.ServerPath);
            loadedFromDefault = true;
        }

        var contents = fileSystem.ReadAllText(sourcePath);
        if (contents is null)
            return AccountLoadResult.Invalid(sourcePath, loadedFromDefault);

        var parsed = AccountFileParser.Parse(accountName, contents, ignoreNickname, parserOptions);
        if (!parsed.Success)
            return AccountLoadResult.Invalid(sourcePath, loadedFromDefault);

        var account = parsed.Account!;
        var requiresGuestIdentityGeneration = IsGuest(accountName);
        if (requiresGuestIdentityGeneration)
            account.IsLoadOnly = true;

        if (loadedFromDefault)
            ApplyDefaultAccountBehavior(accountName, account, settings);
        else
        {
            ClearTransientStatus(account);
            RepairDefaultWeaponStatus(account, settings);
        }

        var shouldSaveCreatedAccount = loadedFromDefault && !account.IsLoadOnly;
        return new AccountLoadResult(
            true,
            account,
            sourcePath,
            loadedFromDefault,
            shouldSaveCreatedAccount,
            shouldSaveCreatedAccount ? $"accounts/{accountName}.txt" : null,
            requiresGuestIdentityGeneration);
    }

    private static void ApplyDefaultAccountBehavior(
        string accountName,
        AccountFileData account,
        IAccountLoadSettings settings)
    {
        if (settings.Exists("startlevel"))
            account.LevelName = settings.GetString("startlevel", "onlinestartlocal.nw");
        if (settings.Exists("startx"))
            account.PixelX = ToPixel(settings.GetFloat("startx", 30.0f));
        if (settings.Exists("starty"))
            account.PixelY = ToPixel(settings.GetFloat("starty", 30.5f));

        account.AccountName = accountName;
        if (!IsGuest(accountName))
            account.CommunityName = accountName;
    }

    private static void RepairDefaultWeaponStatus(AccountFileData account, IAccountLoadSettings settings)
    {
        if (account.Status != 0)
            return;

        if (!GetBool(settings, "defaultweapons", defaultValue: true))
            return;

        if (account.SwordPower == 0 && account.Weapons.Count == 0)
            return;

        account.Status = 20;
    }

    private static void ClearTransientStatus(AccountFileData account)
    {
        const int paused = 0x01;
        account.Status &= ~paused;
    }

    private static string BuildDefaultAccountPath(string serverPath)
    {
        var trimmed = serverPath.TrimEnd('/', '\\');
        return string.Join(
            Path.DirectorySeparatorChar,
            trimmed,
            "accounts",
            "defaultaccount.txt");
    }

    private static short ToPixel(float value) =>
        unchecked((short)(value * 16));

    private static bool IsGuest(string accountName) =>
        string.Equals(accountName, "guest", StringComparison.OrdinalIgnoreCase);

    private static bool GetBool(IAccountLoadSettings settings, string key, bool defaultValue)
    {
        var value = settings.GetString(key, defaultValue ? "true" : "false");
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
    }
}

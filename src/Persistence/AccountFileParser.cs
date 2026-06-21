using System.Globalization;

namespace Preagonal.GServer.Persistence;

public sealed record AccountParseResult(bool Success, AccountFileData? Account)
{
    public static AccountParseResult Invalid { get; } = new(false, null);
}

public sealed record AccountParserOptions(
    int HeartLimit = 3,
    int ShieldLimit = 3,
    int SwordLimit = 3,
    bool HealSwords = false,
    bool CropFlags = true);

public sealed class AccountFileData
{
    public string AccountName { get; set; } = string.Empty;
    public string CommunityName { get; set; } = string.Empty;
    public string Nickname { get; set; } = "default";
    public string LevelName { get; set; } = string.Empty;
    public short PixelX { get; set; }
    public short PixelY { get; set; }
    public short PixelZ { get; set; }
    public byte MaxHitpoints { get; set; } = 3;
    public float Hitpoints { get; set; } = 3.0f;
    public int Rupees { get; set; }
    public string Gani { get; set; } = "idle";
    public byte Arrows { get; set; } = 5;
    public byte Bombs { get; set; } = 10;
    public byte GlovePower { get; set; } = 1;
    public byte ShieldPower { get; set; } = 1;
    public byte SwordPower { get; set; } = 1;
    public byte BowPower { get; set; } = 1;
    public string BowImage { get; set; } = "bow1.png";
    public string HeadImage { get; set; } = "head0.png";
    public string BodyImage { get; set; } = "body.png";
    public string SwordImage { get; set; } = "sword1.png";
    public string ShieldImage { get; set; } = "shield1.png";
    public byte[] Colors { get; } = [2, 0, 10, 4, 18];
    public byte Sprite { get; set; } = 2;
    public int Status { get; set; } = 20;
    public byte MagicPoints { get; set; }
    public byte Alignment { get; set; } = 50;
    public byte ApCounter { get; set; }
    public int OnlineSeconds { get; set; }
    public uint AccountIp { get; set; }
    public string Language { get; set; } = "English";
    public uint Kills { get; set; }
    public uint Deaths { get; set; }
    public float EloRating { get; set; } = 1500.0f;
    public float EloDeviation { get; set; } = 350.0f;
    public long LastSparTime { get; set; }
    public string[] GaniAttributes { get; } = new string[30];
    public List<string> Weapons { get; } = [];
    public List<string> Chests { get; } = [];
    public bool IsBanned { get; set; }
    public string BanReason { get; set; } = string.Empty;
    public string BanLength { get; set; } = string.Empty;
    public string Comments { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int AdminRights { get; set; }
    public string AdminIp { get; set; } = "0.0.0.0";
    public bool IsLoadOnly { get; set; }
    public List<string> FolderRights { get; } = [];
    public string LastFolder { get; set; } = string.Empty;
    public Dictionary<string, string> Flags { get; } = new(StringComparer.Ordinal);
}

public static class AccountFileParser
{
    public static AccountParseResult Parse(
        string accountName,
        string fileContents,
        bool ignoreNickname = false,
        AccountParserOptions? options = null)
    {
        options ??= new AccountParserOptions();
        var lines = fileContents.Split('\n');
        if (lines.Length == 0 || TrimCString(lines[0]) != "GRACC001")
            return AccountParseResult.Invalid;

        var account = new AccountFileData
        {
            AccountName = accountName
        };

        foreach (var rawLine in lines)
        {
            var line = TrimCString(rawLine);
            var separator = line.IndexOf(' ');
            var section = separator == -1 ? line : line[..separator];
            var value = separator == -1 ? string.Empty : line[(separator + 1)..];

            switch (section)
            {
                case "NAME":
                    break;
                case "NICK":
                    if (!ignoreNickname)
                        account.Nickname = Truncate(value, 223);
                    break;
                case "COMMUNITYNAME":
                    account.CommunityName = value;
                    break;
                case "LEVEL":
                    account.LevelName = value;
                    break;
                case "X":
                    account.PixelX = ToPixel(value);
                    break;
                case "Y":
                    account.PixelY = ToPixel(value);
                    break;
                case "Z":
                    account.PixelZ = ToPixel(value);
                    break;
                case "MAXHP":
                    account.MaxHitpoints = (byte)Clip(Atoi(value), 0, Math.Min(options.HeartLimit, 20));
                    break;
                case "HP":
                    account.Hitpoints = Clip(Strtof(value), 0.0f, account.MaxHitpoints);
                    break;
                case "RUPEES":
                    account.Rupees = Atoi(value);
                    break;
                case "ANI":
                    account.Gani = Truncate(value, 223);
                    break;
                case "ARROWS":
                    account.Arrows = ToByteUnchecked(Atoi(value));
                    break;
                case "BOMBS":
                    account.Bombs = ToByteUnchecked(Atoi(value));
                    break;
                case "GLOVEP":
                    account.GlovePower = ToByteUnchecked(Atoi(value));
                    break;
                case "SHIELDP":
                    account.ShieldPower = (byte)Clip(Atoi(value), 0, options.ShieldLimit);
                    break;
                case "SWORDP":
                    var lower = options.HealSwords ? -options.SwordLimit : 0;
                    account.SwordPower = ToByteUnchecked(Clip(Atoi(value), lower, options.SwordLimit));
                    break;
                case "BOWP":
                    account.BowPower = ToByteUnchecked(Atoi(value));
                    break;
                case "BOW":
                    account.BowImage = value;
                    break;
                case "HEAD":
                    account.HeadImage = Truncate(value, 123);
                    break;
                case "BODY":
                    account.BodyImage = Truncate(value, 223);
                    break;
                case "SWORD":
                    account.SwordImage = Truncate(value, 223);
                    break;
                case "SHIELD":
                    account.ShieldImage = Truncate(value, 223);
                    break;
                case "COLORS":
                    ParseColors(account, value);
                    break;
                case "SPRITE":
                    account.Sprite = ToByteUnchecked(Atoi(value));
                    break;
                case "STATUS":
                    account.Status = Atoi(value);
                    break;
                case "MP":
                    account.MagicPoints = ToByteUnchecked(Atoi(value));
                    break;
                case "AP":
                    account.Alignment = ToByteUnchecked(Atoi(value));
                    break;
                case "APCOUNTER":
                    account.ApCounter = ToByteUnchecked(Atoi(value));
                    break;
                case "ONSECS":
                    account.OnlineSeconds = Atoi(value);
                    break;
                case "IP":
                    if (account.AccountIp == 0)
                        account.AccountIp = unchecked((uint)Atol(value));
                    break;
                case "LANGUAGE":
                    account.Language = value.Length == 0 ? "English" : value;
                    break;
                case "KILLS":
                    account.Kills = unchecked((uint)Atoi(value));
                    break;
                case "DEATHS":
                    account.Deaths = unchecked((uint)Atoi(value));
                    break;
                case "RATING":
                    account.EloRating = Strtof(value);
                    break;
                case "DEVIATION":
                    account.EloDeviation = Strtof(value);
                    break;
                case "LASTSPARTIME":
                    account.LastSparTime = Atol(value);
                    break;
                case "FLAG":
                    SetFlag(account, value, options);
                    break;
                case "WEAPON":
                    account.Weapons.Add(value);
                    break;
                case "CHEST":
                    account.Chests.Add(value);
                    break;
                case "BANNED":
                    account.IsBanned = Atoi(value) != 0;
                    break;
                case "BANREASON":
                    account.BanReason = value;
                    break;
                case "BANLENGTH":
                    account.BanLength = value;
                    break;
                case "COMMENTS":
                    account.Comments = value;
                    break;
                case "EMAIL":
                    account.Email = value;
                    break;
                case "LOCALRIGHTS":
                    account.AdminRights = Atoi(value);
                    break;
                case "IPRANGE":
                    account.AdminIp = value;
                    break;
                case "LOADONLY":
                    account.IsLoadOnly = Atoi(value) != 0;
                    break;
                case "FOLDERRIGHT":
                    account.FolderRights.Add(value);
                    break;
                case "LASTFOLDER":
                    account.LastFolder = value;
                    break;
                default:
                    TrySetAttribute(account, section, value);
                    break;
            }
        }

        account.CommunityName = string.Equals(accountName, "guest", StringComparison.OrdinalIgnoreCase)
            ? "guest"
            : account.AccountName;

        return new AccountParseResult(true, account);
    }

    private static void ParseColors(AccountFileData account, string value)
    {
        var parts = value.Split(',');
        for (var i = 0; i < parts.Length && i < 5; i++)
            account.Colors[i] = ToByteUnchecked(Atoi(parts[i]));
    }

    private static void TrySetAttribute(AccountFileData account, string section, string value)
    {
        if (!section.StartsWith("ATTR", StringComparison.Ordinal))
            return;

        var index = Atoi(section[4..]) - 1;
        if (index is >= 0 and < 30)
            account.GaniAttributes[index] = value;
    }

    private static void SetFlag(AccountFileData account, string value, AccountParserOptions options)
    {
        var separator = value.IndexOf('=');
        var name = separator == -1 ? value : value[..separator];
        var flagValue = separator == -1 ? string.Empty : value[(separator + 1)..];
        if (options.CropFlags)
        {
            var fixedLength = 223 - 1 - name.Length;
            flagValue = Truncate(flagValue, Math.Max(fixedLength, 0));
        }
        account.Flags[name] = flagValue;
    }

    private static short ToPixel(string value) =>
        unchecked((short)(Strtof(value) * 16));

    private static byte ToByteUnchecked(int value) =>
        unchecked((byte)value);

    private static int Clip(int value, int min, int max) =>
        Math.Min(Math.Max(value, min), max);

    private static float Clip(float value, float min, float max) =>
        Math.Min(Math.Max(value, min), max);

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..length];

    private static string TrimCString(string value) =>
        value.Trim();

    private static int Atoi(string value)
    {
        var parsed = ParseIntegerPrefix(value);
        return unchecked((int)parsed);
    }

    private static long Atol(string value) =>
        ParseIntegerPrefix(value);

    private static long ParseIntegerPrefix(string value)
    {
        var index = 0;
        while (index < value.Length && char.IsWhiteSpace(value[index]))
            index++;

        var sign = 1L;
        if (index < value.Length && (value[index] == '-' || value[index] == '+'))
        {
            sign = value[index] == '-' ? -1L : 1L;
            index++;
        }

        var result = 0L;
        while (index < value.Length && char.IsAsciiDigit(value[index]))
        {
            result = unchecked(result * 10 + (value[index] - '0'));
            index++;
        }

        return unchecked(result * sign);
    }

    private static float Strtof(string value)
    {
        var trimmed = value.TrimStart();
        var length = 0;
        while (length < trimmed.Length && IsFloatPrefixChar(trimmed[length], length))
            length++;

        if (length == 0)
            return 0;

        return float.TryParse(trimmed[..length], NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0;
    }

    private static bool IsFloatPrefixChar(char value, int index) =>
        char.IsAsciiDigit(value) || value == '.' || value == 'e' || value == 'E' ||
        ((value == '-' || value == '+') && index == 0);
}

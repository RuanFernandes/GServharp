using System.Globalization;
using System.Text;

namespace GServ.Persistence;

public sealed record AccountFileSerializeResult(bool Success, string Contents)
{
    public static AccountFileSerializeResult LoadOnlyBlocked { get; } = new(false, string.Empty);
}

public static class AccountFileSerializer
{
    public static AccountFileSerializeResult Serialize(AccountFileData account)
    {
        if (account.IsLoadOnly)
            return AccountFileSerializeResult.LoadOnlyBlocked;

        var builder = new StringBuilder();
        builder.Append("GRACC001\r\n");
        AppendLine(builder, "NAME", account.AccountName);
        AppendLine(builder, "NICK", account.Nickname);
        AppendLine(builder, "COMMUNITYNAME", account.AccountName);
        AppendLine(builder, "LEVEL", account.LevelName);
        AppendLine(builder, "X", FormatCoordinate(account.PixelX));
        AppendLine(builder, "Y", FormatCoordinate(account.PixelY));
        AppendLine(builder, "Z", FormatCoordinate(account.PixelZ));
        AppendLine(builder, "MAXHP", Format(account.MaxHitpoints));
        AppendLine(builder, "HP", Format(account.Hitpoints));
        AppendLine(builder, "RUPEES", Format(account.Rupees));
        AppendLine(builder, "ANI", account.Gani);
        AppendLine(builder, "ARROWS", Format(account.Arrows));
        AppendLine(builder, "BOMBS", Format(account.Bombs));
        AppendLine(builder, "GLOVEP", Format(account.GlovePower));
        AppendLine(builder, "SHIELDP", Format(account.ShieldPower));
        AppendLine(builder, "SWORDP", Format(account.SwordPower));
        AppendLine(builder, "BOWP", Format(account.BowPower));
        AppendLine(builder, "BOW", account.BowImage);
        AppendLine(builder, "HEAD", account.HeadImage);
        AppendLine(builder, "BODY", account.BodyImage);
        AppendLine(builder, "SWORD", account.SwordImage);
        AppendLine(builder, "SHIELD", account.ShieldImage);
        AppendLine(builder, "COLORS", string.Join(',', account.Colors.Select(static color => Format(color))));
        AppendLine(builder, "SPRITE", Format(account.Sprite));
        AppendLine(builder, "STATUS", Format(account.Status));
        AppendLine(builder, "MP", Format(account.MagicPoints));
        AppendLine(builder, "AP", Format(account.Alignment));
        AppendLine(builder, "APCOUNTER", Format(account.ApCounter));
        AppendLine(builder, "ONSECS", Format(account.OnlineSeconds));
        AppendLine(builder, "IP", Format(account.AccountIp));
        AppendLine(builder, "LANGUAGE", account.Language);
        AppendLine(builder, "KILLS", Format(account.Kills));
        AppendLine(builder, "DEATHS", Format(account.Deaths));
        AppendLine(builder, "RATING", Format(account.EloRating));
        AppendLine(builder, "DEVIATION", Format(account.EloDeviation));
        AppendLine(builder, "LASTSPARTIME", Format(account.LastSparTime));

        for (var i = 0; i < account.GaniAttributes.Length; i++)
        {
            if (!string.IsNullOrEmpty(account.GaniAttributes[i]))
                AppendLine(builder, $"ATTR{i + 1}", account.GaniAttributes[i]);
        }

        foreach (var chest in account.Chests)
            AppendLine(builder, "CHEST", chest);

        foreach (var weapon in account.Weapons)
            AppendLine(builder, "WEAPON", weapon);

        foreach (var flag in account.Flags)
        {
            builder.Append("FLAG ").Append(flag.Key);
            if (!string.IsNullOrEmpty(flag.Value))
                builder.Append('=').Append(flag.Value);
            builder.Append("\r\n");
        }

        builder.Append("\r\n");
        AppendLine(builder, "BANNED", Format(account.IsBanned));
        AppendLine(builder, "BANREASON", account.BanReason);
        AppendLine(builder, "BANLENGTH", account.BanLength);
        AppendLine(builder, "COMMENTS", account.Comments);
        AppendLine(builder, "EMAIL", account.Email);
        AppendLine(builder, "LOCALRIGHTS", Format(account.AdminRights));
        AppendLine(builder, "IPRANGE", account.AdminIp);
        AppendLine(builder, "LOADONLY", Format(account.IsLoadOnly));

        foreach (var folderRight in account.FolderRights)
            AppendLine(builder, "FOLDERRIGHT", folderRight);

        AppendLine(builder, "LASTFOLDER", account.LastFolder);
        return new AccountFileSerializeResult(true, builder.ToString());
    }

    private static void AppendLine(StringBuilder builder, string field, string value) =>
        builder.Append(field).Append(' ').Append(value).Append("\r\n");

    private static string FormatCoordinate(short pixelCoordinate) =>
        Format(pixelCoordinate / 16.0f);

    private static string Format(bool value) =>
        value ? "1" : "0";

    private static string Format(byte value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string Format(int value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string Format(uint value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string Format(long value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string Format(float value) =>
        value.ToString("G9", CultureInfo.InvariantCulture);
}

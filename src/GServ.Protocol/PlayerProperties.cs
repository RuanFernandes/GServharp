using System.Text;

namespace GServ.Protocol;

public enum PlayerPropertyId : byte
{
    Nickname = 0,
    MaxPower = 1,
    CurrentPower = 2,
    RupeesCount = 3,
    ArrowsCount = 4,
    BombsCount = 5,
    GlovePower = 6,
    BombPower = 7,
    SwordPower = 8,
    ShieldPower = 9,
    Gani = 10,
    HeadGif = 11,
    CurrentChat = 12,
    Colors = 13,
    Id = 14,
    X = 15,
    Y = 16,
    Sprite = 17,
    Status = 18,
    CarrySprite = 19,
    CurrentLevel = 20,
    HorseGif = 21,
    HorseBushes = 22,
    EffectColors = 23,
    CarryNpc = 24,
    ApCounter = 25,
    MagicPoints = 26,
    KillsCount = 27,
    DeathsCount = 28,
    OnlineSeconds = 29,
    IpAddress = 30,
    Alignment = 32,
    AdditionalFlags = 33,
    AccountName = 34,
    BodyImage = 35,
    Rating = 36,
    GAttrib1 = 37,
    GAttrib2 = 38,
    GAttrib3 = 39,
    GAttrib4 = 40,
    GAttrib5 = 41,
    GAttrib6 = 46,
    GAttrib7 = 47,
    GAttrib8 = 48,
    GAttrib9 = 49,
    GAttrib10 = 54,
    GAttrib11 = 55,
    GAttrib12 = 56,
    GAttrib13 = 57,
    GAttrib14 = 58,
    GAttrib15 = 59,
    GAttrib16 = 60,
    GAttrib17 = 61,
    GAttrib18 = 62,
    GAttrib19 = 63,
    GAttrib20 = 64,
    GAttrib21 = 65,
    GAttrib22 = 66,
    GAttrib23 = 67,
    GAttrib24 = 68,
    GAttrib25 = 69,
    GAttrib26 = 70,
    GAttrib27 = 71,
    GAttrib28 = 72,
    GAttrib29 = 73,
    GAttrib30 = 74,
    OsType = 75,
    TextCodePage = 76,
    CommunityName = 82
}

public sealed record PlayerPropertySource(
    string Nickname,
    byte MaxPower,
    float Hitpoints,
    int Rupees,
    byte Arrows,
    byte Bombs,
    byte GlovePower,
    byte SwordPower,
    string SwordImage,
    byte ShieldPower,
    string ShieldImage,
    string Gani,
    string HeadImage,
    string ChatMessage,
    IReadOnlyList<byte> Colors,
    ushort PlayerId,
    int X,
    int Y,
    byte Sprite,
    byte Status,
    byte CarrySprite,
    string CurrentLevel,
    string HorseImage,
    byte HorseBombCount,
    int CarryNpcId,
    ushort ApCounter,
    byte MagicPoints,
    int Kills,
    int Deaths,
    int OnlineSeconds,
    uint AccountIp,
    byte Alignment,
    byte AdditionalFlags,
    string AccountName,
    string BodyImage,
    int EloRating,
    int EloDeviation,
    IReadOnlyDictionary<int, string> GaniAttributes,
    string Os,
    uint TextCodePage,
    string CommunityName);

public static class SendLoginPropertySet
{
    public static readonly IReadOnlyList<PlayerPropertyId> All =
    [
        PlayerPropertyId.MaxPower,
        PlayerPropertyId.CurrentPower,
        PlayerPropertyId.RupeesCount,
        PlayerPropertyId.ArrowsCount,
        PlayerPropertyId.BombsCount,
        PlayerPropertyId.GlovePower,
        PlayerPropertyId.SwordPower,
        PlayerPropertyId.ShieldPower,
        PlayerPropertyId.Gani,
        PlayerPropertyId.HeadGif,
        PlayerPropertyId.Colors,
        PlayerPropertyId.Sprite,
        PlayerPropertyId.Status,
        PlayerPropertyId.HorseGif,
        PlayerPropertyId.HorseBushes,
        PlayerPropertyId.EffectColors,
        PlayerPropertyId.ApCounter,
        PlayerPropertyId.MagicPoints,
        PlayerPropertyId.Alignment,
        PlayerPropertyId.AccountName,
        PlayerPropertyId.BodyImage,
        PlayerPropertyId.Rating,
        PlayerPropertyId.GAttrib1,
        PlayerPropertyId.GAttrib2,
        PlayerPropertyId.GAttrib3,
        PlayerPropertyId.GAttrib4,
        PlayerPropertyId.GAttrib5,
        PlayerPropertyId.GAttrib6,
        PlayerPropertyId.GAttrib7,
        PlayerPropertyId.GAttrib8,
        PlayerPropertyId.GAttrib9,
        PlayerPropertyId.GAttrib10,
        PlayerPropertyId.GAttrib11,
        PlayerPropertyId.GAttrib12,
        PlayerPropertyId.GAttrib13,
        PlayerPropertyId.GAttrib14,
        PlayerPropertyId.GAttrib15,
        PlayerPropertyId.GAttrib16,
        PlayerPropertyId.GAttrib17,
        PlayerPropertyId.GAttrib18,
        PlayerPropertyId.GAttrib19,
        PlayerPropertyId.GAttrib20,
        PlayerPropertyId.GAttrib21,
        PlayerPropertyId.GAttrib22,
        PlayerPropertyId.GAttrib23,
        PlayerPropertyId.GAttrib24,
        PlayerPropertyId.GAttrib25,
        PlayerPropertyId.GAttrib26,
        PlayerPropertyId.GAttrib27,
        PlayerPropertyId.GAttrib28,
        PlayerPropertyId.GAttrib29,
        PlayerPropertyId.GAttrib30,
        PlayerPropertyId.CommunityName
    ];

    private static readonly IReadOnlyList<PlayerPropertyId> PreClient21 =
        All.Where(id => (byte)id < 37).ToArray();

    public static IReadOnlyList<PlayerPropertyId> ForClient(bool preClient21) =>
        preClient21 ? PreClient21 : All;
}

public static class PlayerPropertySerializer
{
    private static readonly int[] AttributePropertyIds =
    [
        37, 38, 39, 40, 41, 46, 47, 48, 49, 54,
        55, 56, 57, 58, 59, 60, 61, 62, 63, 64,
        65, 66, 67, 68, 69, 70, 71, 72, 73, 74
    ];

    public static byte[] SerializeConfirmedLoginSubset(
        PlayerPropertySource source,
        IEnumerable<PlayerPropertyId> propertyIds)
    {
        var writer = new GraalBinaryWriter();
        foreach (var propertyId in propertyIds.Distinct().OrderBy(id => (byte)id))
        {
            writer.WriteGChar((byte)propertyId);
            WritePropertyValue(writer, source, propertyId);
        }

        return writer.ToArray();
    }

    public static byte[] BuildPlayerPropsPacket(ReadOnlySpan<byte> propertyPayload, bool appendNewline = false)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.PlayerProps);
        writer.WriteBytes(propertyPayload);
        if (appendNewline)
            writer.WriteByte((byte)'\n');
        return writer.ToArray();
    }

    private static void WritePropertyValue(GraalBinaryWriter writer, PlayerPropertySource source, PlayerPropertyId propertyId)
    {
        switch (propertyId)
        {
            case PlayerPropertyId.Nickname:
                WriteGCharString(writer, source.Nickname);
                return;
            case PlayerPropertyId.MaxPower:
                writer.WriteGChar(source.MaxPower);
                return;
            case PlayerPropertyId.CurrentPower:
                writer.WriteGChar((byte)(source.Hitpoints * 2));
                return;
            case PlayerPropertyId.RupeesCount:
                writer.WriteGInt((uint)source.Rupees);
                return;
            case PlayerPropertyId.ArrowsCount:
                writer.WriteGChar(source.Arrows);
                return;
            case PlayerPropertyId.BombsCount:
                writer.WriteGChar(source.Bombs);
                return;
            case PlayerPropertyId.GlovePower:
                writer.WriteGChar(source.GlovePower);
                return;
            case PlayerPropertyId.SwordPower:
                writer.WriteGChar((byte)(source.SwordPower + 30));
                WriteGCharString(writer, source.SwordImage);
                return;
            case PlayerPropertyId.ShieldPower:
                writer.WriteGChar((byte)(source.ShieldPower + 10));
                WriteGCharString(writer, source.ShieldImage);
                return;
            case PlayerPropertyId.Gani:
                WriteGCharString(writer, source.Gani);
                return;
            case PlayerPropertyId.HeadGif:
                writer.WriteGChar((byte)(source.HeadImage.Length + 100));
                writer.WriteBytes(Encoding.ASCII.GetBytes(source.HeadImage));
                return;
            case PlayerPropertyId.CurrentChat:
                WriteGCharString(writer, source.ChatMessage);
                return;
            case PlayerPropertyId.Colors:
                foreach (var color in source.Colors.Take(5))
                    writer.WriteGChar(color);
                return;
            case PlayerPropertyId.Id:
                writer.WriteGShort(source.PlayerId);
                return;
            case PlayerPropertyId.X:
                writer.WriteGChar((byte)(source.X / 8));
                return;
            case PlayerPropertyId.Y:
                writer.WriteGChar((byte)(source.Y / 8));
                return;
            case PlayerPropertyId.Sprite:
                writer.WriteGChar(source.Sprite);
                return;
            case PlayerPropertyId.Status:
                writer.WriteGChar(source.Status);
                return;
            case PlayerPropertyId.CarrySprite:
                writer.WriteGChar(source.CarrySprite);
                return;
            case PlayerPropertyId.CurrentLevel:
                WriteGCharString(writer, source.CurrentLevel);
                return;
            case PlayerPropertyId.HorseGif:
                WriteGCharString(writer, source.HorseImage);
                return;
            case PlayerPropertyId.HorseBushes:
                writer.WriteGChar(source.HorseBombCount);
                return;
            case PlayerPropertyId.EffectColors:
                writer.WriteGChar(0);
                return;
            case PlayerPropertyId.CarryNpc:
                writer.WriteGInt((uint)source.CarryNpcId);
                return;
            case PlayerPropertyId.ApCounter:
                writer.WriteGShort((ushort)(source.ApCounter + 1));
                return;
            case PlayerPropertyId.MagicPoints:
                writer.WriteGChar(source.MagicPoints);
                return;
            case PlayerPropertyId.KillsCount:
                writer.WriteGInt((uint)source.Kills);
                return;
            case PlayerPropertyId.DeathsCount:
                writer.WriteGInt((uint)source.Deaths);
                return;
            case PlayerPropertyId.OnlineSeconds:
                writer.WriteGInt((uint)source.OnlineSeconds);
                return;
            case PlayerPropertyId.IpAddress:
                writer.WriteGInt5(source.AccountIp);
                return;
            case PlayerPropertyId.Alignment:
                writer.WriteGChar(source.Alignment);
                return;
            case PlayerPropertyId.AdditionalFlags:
                writer.WriteGChar(source.AdditionalFlags);
                return;
            case PlayerPropertyId.AccountName:
                WriteGCharString(writer, source.AccountName);
                return;
            case PlayerPropertyId.BodyImage:
                WriteGCharString(writer, source.BodyImage);
                return;
            case PlayerPropertyId.Rating:
                writer.WriteGInt((uint)(((source.EloRating & 0xFFF) << 9) | (source.EloDeviation & 0x1FF)));
                return;
            case PlayerPropertyId.OsType:
                WriteGCharString(writer, source.Os);
                return;
            case PlayerPropertyId.TextCodePage:
                writer.WriteGInt(source.TextCodePage);
                return;
            case PlayerPropertyId.CommunityName:
                WriteGCharString(writer, source.CommunityName);
                return;
            default:
                if (TryWriteGaniAttribute(writer, source, (byte)propertyId))
                    return;
                throw new NotSupportedException($"Player property {(byte)propertyId} is not confirmed in the serializer subset.");
        }
    }

    private static bool TryWriteGaniAttribute(GraalBinaryWriter writer, PlayerPropertySource source, int propertyId)
    {
        var index = Array.IndexOf(AttributePropertyIds, propertyId);
        if (index < 0)
            return false;

        source.GaniAttributes.TryGetValue(propertyId, out var value);
        value ??= string.Empty;
        var truncated = value.Length > 223 ? value[..223] : value;
        WriteGCharString(writer, truncated);
        return true;
    }

    private static void WriteGCharString(GraalBinaryWriter writer, string value)
    {
        writer.WriteGChar((byte)value.Length);
        writer.WriteBytes(Encoding.ASCII.GetBytes(value));
    }
}

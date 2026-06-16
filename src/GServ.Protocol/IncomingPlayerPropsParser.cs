using System.Text;

namespace GServ.Protocol;

public sealed record IncomingPlayerPropsParseResult(
    bool Success,
    IReadOnlyList<IncomingPlayerPropertyUpdate> Updates,
    PlayerPropertyId? UnsupportedPropertyId)
{
    public static IncomingPlayerPropsParseResult Ok(IReadOnlyList<IncomingPlayerPropertyUpdate> updates) =>
        new(true, updates, null);

    public static IncomingPlayerPropsParseResult Unsupported(
        IReadOnlyList<IncomingPlayerPropertyUpdate> updates,
        PlayerPropertyId propertyId) =>
        new(false, updates, propertyId);
}

public sealed record IncomingPlayerPropertyUpdate(
    PlayerPropertyId PropertyId,
    byte? GCharValue = null,
    ushort? GShortValue = null,
    int? GIntValue = null,
    string? StringValue = null,
    IReadOnlyList<byte>? BytesValue = null)
{
    public static IncomingPlayerPropertyUpdate GChar(PlayerPropertyId propertyId, byte value) =>
        new(propertyId, GCharValue: value);

    public static IncomingPlayerPropertyUpdate GShort(PlayerPropertyId propertyId, ushort value) =>
        new(propertyId, GShortValue: value);

    public static IncomingPlayerPropertyUpdate GInt(PlayerPropertyId propertyId, int value) =>
        new(propertyId, GIntValue: value);

    public static IncomingPlayerPropertyUpdate String(PlayerPropertyId propertyId, string value) =>
        new(propertyId, StringValue: value);

    public static IncomingPlayerPropertyUpdate Bytes(PlayerPropertyId propertyId, IReadOnlyList<byte> value) =>
        new(propertyId, BytesValue: value);

    public static IncomingPlayerPropertyUpdate NoValue(PlayerPropertyId propertyId) =>
        new(propertyId);
}

public static class IncomingPlayerPropsParser
{
    public static IncomingPlayerPropsParseResult Parse(ReadOnlySpan<byte> body)
    {
        var reader = new GraalBinaryReader(body);
        var updates = new List<IncomingPlayerPropertyUpdate>();

        while (reader.BytesLeft > 0)
        {
            var propertyId = (PlayerPropertyId)reader.ReadGChar();
            switch (propertyId)
            {
                case PlayerPropertyId.X:
                case PlayerPropertyId.Y:
                case PlayerPropertyId.Z:
                case PlayerPropertyId.Sprite:
                case PlayerPropertyId.MaxPower:
                case PlayerPropertyId.CurrentPower:
                case PlayerPropertyId.ArrowsCount:
                case PlayerPropertyId.BombsCount:
                case PlayerPropertyId.GlovePower:
                case PlayerPropertyId.BombPower:
                case PlayerPropertyId.MagicPoints:
                case PlayerPropertyId.Alignment:
                case PlayerPropertyId.AdditionalFlags:
                case PlayerPropertyId.CarrySprite:
                case PlayerPropertyId.HorseBushes:
                    updates.Add(IncomingPlayerPropertyUpdate.GChar(propertyId, reader.ReadGChar()));
                    break;

                case PlayerPropertyId.RupeesCount:
                    updates.Add(IncomingPlayerPropertyUpdate.GInt(propertyId, Math.Min(reader.ReadGInt(), 9_999_999)));
                    break;

                case PlayerPropertyId.CurrentLevel:
                case PlayerPropertyId.Gani:
                case PlayerPropertyId.BodyImage:
                case PlayerPropertyId.PlayerLanguage:
                case PlayerPropertyId.OsType:
                    updates.Add(IncomingPlayerPropertyUpdate.String(propertyId, ReadGCharString(reader)));
                    break;

                case PlayerPropertyId.AccountName:
                case PlayerPropertyId.CommunityName:
                    ReadGCharString(reader);
                    updates.Add(IncomingPlayerPropertyUpdate.NoValue(propertyId));
                    break;

                case PlayerPropertyId.Colors:
                    updates.Add(IncomingPlayerPropertyUpdate.Bytes(
                        propertyId,
                        [reader.ReadGChar(), reader.ReadGChar(), reader.ReadGChar(), reader.ReadGChar(), reader.ReadGChar()]));
                    break;

                case PlayerPropertyId.EffectColors:
                    var effectColorLength = reader.ReadGChar();
                    if (effectColorLength > 0)
                        reader.ReadGInt4();
                    updates.Add(IncomingPlayerPropertyUpdate.NoValue(propertyId));
                    break;

                case PlayerPropertyId.TextCodePage:
                    updates.Add(IncomingPlayerPropertyUpdate.GInt(propertyId, reader.ReadGInt()));
                    break;

                case PlayerPropertyId.X2:
                case PlayerPropertyId.Y2:
                case PlayerPropertyId.Z2:
                case PlayerPropertyId.ApCounter:
                    updates.Add(IncomingPlayerPropertyUpdate.GShort(propertyId, reader.ReadGShort()));
                    break;

                case PlayerPropertyId.Id:
                    reader.ReadGShort();
                    updates.Add(IncomingPlayerPropertyUpdate.NoValue(propertyId));
                    break;

                case PlayerPropertyId.KillsCount:
                case PlayerPropertyId.DeathsCount:
                case PlayerPropertyId.OnlineSeconds:
                case PlayerPropertyId.Rating:
                    reader.ReadGInt();
                    updates.Add(IncomingPlayerPropertyUpdate.NoValue(propertyId));
                    break;

                case PlayerPropertyId.JoinLeaveLevel:
                case PlayerPropertyId.PlayerConnected:
                    updates.Add(IncomingPlayerPropertyUpdate.NoValue(propertyId));
                    break;

                case PlayerPropertyId.Unknown81:
                    reader.ReadGChar();
                    updates.Add(IncomingPlayerPropertyUpdate.NoValue(propertyId));
                    break;

                default:
                    if (IsGaniAttributeProperty(propertyId))
                    {
                        updates.Add(IncomingPlayerPropertyUpdate.String(propertyId, ReadGCharString(reader)));
                        break;
                    }

                    return IncomingPlayerPropsParseResult.Unsupported(updates, propertyId);
            }
        }

        return IncomingPlayerPropsParseResult.Ok(updates);
    }

    private static string ReadGCharString(GraalBinaryReader reader)
    {
        var length = reader.ReadGChar();
        return Encoding.ASCII.GetString(reader.ReadBytes(length));
    }

    private static bool IsGaniAttributeProperty(PlayerPropertyId propertyId) =>
        propertyId is >= PlayerPropertyId.GAttrib1 and <= PlayerPropertyId.GAttrib5
            or >= PlayerPropertyId.GAttrib6 and <= PlayerPropertyId.GAttrib9
            or >= PlayerPropertyId.GAttrib10 and <= PlayerPropertyId.GAttrib30;
}

public static class IncomingPlayerPropsForwarding
{
    public static byte[] BuildOtherPlayerPropsPacket(
        ushort playerId,
        int pixelX,
        int pixelY,
        int pixelZ,
        IEnumerable<IncomingPlayerPropertyUpdate> updates,
        bool senderSupportsPreciseMovement,
        bool appendNewline = false)
    {
        var levelBuff = new GraalBinaryWriter();
        var levelBuff2 = new GraalBinaryWriter();

        foreach (var update in updates)
        {
            switch (update.PropertyId)
            {
                case PlayerPropertyId.X:
                    WriteProperty(levelBuff2, PlayerPropertyId.X2, writer => WriteSignedCoordinate(writer, pixelX));
                    WriteProperty(levelBuff, PlayerPropertyId.X, writer => writer.WriteGChar((byte)(pixelX / 8)));
                    break;

                case PlayerPropertyId.Y:
                    WriteProperty(levelBuff2, PlayerPropertyId.Y2, writer => WriteSignedCoordinate(writer, pixelY));
                    WriteProperty(levelBuff, PlayerPropertyId.Y, writer => writer.WriteGChar((byte)(pixelY / 8)));
                    break;

                case PlayerPropertyId.Z:
                    WriteProperty(levelBuff2, PlayerPropertyId.Z2, writer => WriteSignedCoordinate(writer, pixelZ));
                    WriteProperty(levelBuff, PlayerPropertyId.Z, writer => writer.WriteGChar((byte)((pixelZ / 8) + 50)));
                    break;

                case PlayerPropertyId.X2:
                    WriteProperty(levelBuff2, PlayerPropertyId.X, writer => writer.WriteGChar((byte)(pixelX / 8)));
                    WriteProperty(levelBuff, PlayerPropertyId.X2, writer => WriteSignedCoordinate(writer, pixelX));
                    break;

                case PlayerPropertyId.Y2:
                    WriteProperty(levelBuff2, PlayerPropertyId.Y, writer => writer.WriteGChar((byte)(pixelY / 8)));
                    WriteProperty(levelBuff, PlayerPropertyId.Y2, writer => WriteSignedCoordinate(writer, pixelY));
                    break;

                case PlayerPropertyId.Z2:
                    WriteProperty(levelBuff2, PlayerPropertyId.Z, writer => writer.WriteGChar((byte)((pixelZ / 8) + 50)));
                    WriteProperty(levelBuff, PlayerPropertyId.Z2, writer => WriteSignedCoordinate(writer, pixelZ));
                    break;

                case PlayerPropertyId.Sprite:
                    WriteProperty(levelBuff, PlayerPropertyId.Sprite, writer => writer.WriteGChar(update.GCharValue.GetValueOrDefault()));
                    break;

                case PlayerPropertyId.CurrentLevel:
                    WriteProperty(levelBuff, PlayerPropertyId.CurrentLevel, writer => WriteGCharString(writer, update.StringValue ?? string.Empty));
                    break;

                case PlayerPropertyId.Gani:
                    WriteProperty(levelBuff, PlayerPropertyId.Gani, writer => WriteGCharString(writer, update.StringValue ?? string.Empty));
                    break;

                case PlayerPropertyId.BodyImage:
                    WriteProperty(levelBuff, PlayerPropertyId.BodyImage, writer => WriteGCharString(writer, update.StringValue ?? string.Empty));
                    break;

                case PlayerPropertyId.ApCounter:
                    WriteProperty(levelBuff, PlayerPropertyId.ApCounter, writer => writer.WriteGShort((ushort)(update.GShortValue.GetValueOrDefault() + 1)));
                    break;

                case PlayerPropertyId.Colors:
                    WriteProperty(levelBuff, PlayerPropertyId.Colors, writer =>
                    {
                        foreach (var color in update.BytesValue?.Take(5) ?? [])
                            writer.WriteGChar(color);
                    });
                    break;

                default:
                    if (IsGaniAttributeProperty(update.PropertyId))
                        WriteProperty(levelBuff, update.PropertyId, writer => WriteGCharString(writer, update.StringValue ?? string.Empty));
                    break;
            }
        }

        var first = senderSupportsPreciseMovement ? levelBuff2.ToArray() : levelBuff.ToArray();
        var second = senderSupportsPreciseMovement ? levelBuff.ToArray() : levelBuff2.ToArray();
        var payload = new byte[first.Length + second.Length];
        first.CopyTo(payload, 0);
        second.CopyTo(payload.AsSpan(first.Length));
        return PlayerPropertySerializer.BuildOtherPlayerPropsPacket(playerId, payload, appendNewline);
    }

    private static void WriteProperty(GraalBinaryWriter writer, PlayerPropertyId propertyId, Action<GraalBinaryWriter> writeValue)
    {
        writer.WriteGChar((byte)propertyId);
        writeValue(writer);
    }

    private static void WriteSignedCoordinate(GraalBinaryWriter writer, int value)
    {
        var encoded = (ushort)(Math.Abs(value) << 1);
        if (value < 0)
            encoded |= 0x0001;
        writer.WriteGShort(encoded);
    }

    private static void WriteGCharString(GraalBinaryWriter writer, string value)
    {
        writer.WriteGChar((byte)value.Length);
        writer.WriteBytes(Encoding.ASCII.GetBytes(value));
    }

    private static bool IsGaniAttributeProperty(PlayerPropertyId propertyId) =>
        propertyId is >= PlayerPropertyId.GAttrib1 and <= PlayerPropertyId.GAttrib5
            or >= PlayerPropertyId.GAttrib6 and <= PlayerPropertyId.GAttrib9
            or >= PlayerPropertyId.GAttrib10 and <= PlayerPropertyId.GAttrib30;
}

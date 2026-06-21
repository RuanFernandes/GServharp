using System.Text;

namespace Preagonal.GServer.Protocol;

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
    uint? GUIntValue = null,
    string? StringValue = null,
    IReadOnlyList<byte>? BytesValue = null)
{
    public static IncomingPlayerPropertyUpdate GChar(PlayerPropertyId propertyId, byte value) =>
        new(propertyId, GCharValue: value);

    public static IncomingPlayerPropertyUpdate GShort(PlayerPropertyId propertyId, ushort value) =>
        new(propertyId, GShortValue: value);

    public static IncomingPlayerPropertyUpdate GInt(PlayerPropertyId propertyId, int value) =>
        new(propertyId, GIntValue: value);

    public static IncomingPlayerPropertyUpdate GUInt(PlayerPropertyId propertyId, uint value) =>
        new(propertyId, GIntValue: value <= int.MaxValue ? (int)value : null, GUIntValue: value);

    public static IncomingPlayerPropertyUpdate String(PlayerPropertyId propertyId, string value) =>
        new(propertyId, StringValue: value);

    public static IncomingPlayerPropertyUpdate Bytes(PlayerPropertyId propertyId, IReadOnlyList<byte> value) =>
        new(propertyId, BytesValue: value);

    public static IncomingPlayerPropertyUpdate NoValue(PlayerPropertyId propertyId) =>
        new(propertyId);
}

public static class IncomingPlayerPropsParser
{
    public static IncomingPlayerPropsParseResult Parse(
        ReadOnlySpan<byte> body,
        ClientVersionId clientVersion = ClientVersionId.Client21)
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
                case PlayerPropertyId.Status:
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
                case PlayerPropertyId.PlayerStatusMessage:
                case PlayerPropertyId.GmapLevelX:
                case PlayerPropertyId.GmapLevelY:
                    updates.Add(IncomingPlayerPropertyUpdate.GChar(propertyId, reader.ReadGChar()));
                    break;

                case PlayerPropertyId.SwordPower:
                    updates.Add(ReadSwordPower(reader, clientVersion));
                    break;

                case PlayerPropertyId.ShieldPower:
                    updates.Add(ReadShieldPower(reader, clientVersion));
                    break;

                case PlayerPropertyId.RupeesCount:
                    updates.Add(IncomingPlayerPropertyUpdate.GInt(propertyId, (int)Math.Min(reader.ReadGUInt(), 9_999_999u)));
                    break;

                case PlayerPropertyId.CarryNpc:
                    updates.Add(IncomingPlayerPropertyUpdate.GUInt(propertyId, reader.ReadGUInt()));
                    break;

                case PlayerPropertyId.Nickname:
                case PlayerPropertyId.CurrentLevel:
                case PlayerPropertyId.BodyImage:
                case PlayerPropertyId.PlayerLanguage:
                case PlayerPropertyId.OsType:
                    updates.Add(IncomingPlayerPropertyUpdate.String(propertyId, ReadGCharString(reader)));
                    break;

                case PlayerPropertyId.Gani:
                    updates.Add(ReadGani(reader, clientVersion));
                    break;

                case PlayerPropertyId.HeadGif:
                    updates.Add(ReadHeadImage(reader, clientVersion));
                    break;

                case PlayerPropertyId.CurrentChat:
                    updates.Add(ReadCurrentChat(reader));
                    break;

                case PlayerPropertyId.HorseGif:
                    updates.Add(ReadHorseImage(reader, clientVersion));
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
                case PlayerPropertyId.UdpPort:
                    updates.Add(IncomingPlayerPropertyUpdate.GInt(propertyId, reader.ReadGInt()));
                    break;

                case PlayerPropertyId.AttachNpc:
                    var attachObjectType = reader.ReadGChar();
                    var attachNpcId = reader.ReadGUInt();
                    updates.Add(new IncomingPlayerPropertyUpdate(
                        propertyId,
                        GCharValue: attachObjectType,
                        GIntValue: attachNpcId <= int.MaxValue ? (int)attachNpcId : null,
                        GUIntValue: attachNpcId));
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

                case PlayerPropertyId.IpAddress:
                    reader.ReadGInt5();
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

    private static IncomingPlayerPropertyUpdate ReadGani(
        GraalBinaryReader reader,
        ClientVersionId clientVersion)
    {
        if (clientVersion >= ClientVersionId.Client21)
            return IncomingPlayerPropertyUpdate.String(PlayerPropertyId.Gani, ReadGCharString(reader));

        var sp = reader.ReadGChar();
        if (sp < 10)
            return IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.Gani, sp);

        var imageLength = sp - 10;
        var image = Encoding.ASCII.GetString(reader.ReadBytes(imageLength));
        if (image.Length != 0 && HasNoExtension(image))
            image += ".gif";

        return new IncomingPlayerPropertyUpdate(PlayerPropertyId.Gani, GCharValue: 10, StringValue: image);
    }

    private static IncomingPlayerPropertyUpdate ReadHeadImage(
        GraalBinaryReader reader,
        ClientVersionId clientVersion)
    {
        var length = reader.ReadGChar();
        if (length < 100)
        {
            var extension = clientVersion < ClientVersionId.Client21 ? ".gif" : ".png";
            return IncomingPlayerPropertyUpdate.String(PlayerPropertyId.HeadGif, "head" + length + extension);
        }

        if (length == 100)
            return IncomingPlayerPropertyUpdate.NoValue(PlayerPropertyId.HeadGif);

        var image = Encoding.ASCII.GetString(reader.ReadBytes(length - 100));
        var newline = image.IndexOf('\n', StringComparison.Ordinal);
        if (newline > 0)
            image = image[..newline];

        if (image.Length != 0 && clientVersion < ClientVersionId.Client21 && HasNoExtension(image))
            image += ".gif";

        return IncomingPlayerPropertyUpdate.String(PlayerPropertyId.HeadGif, image);
    }

    private static IncomingPlayerPropertyUpdate ReadHorseImage(
        GraalBinaryReader reader,
        ClientVersionId clientVersion)
    {
        var length = reader.ReadGChar();
        var image = Encoding.ASCII.GetString(reader.ReadBytes(Math.Min((int)length, 219)));
        if (image.Length != 0 && clientVersion < ClientVersionId.Client21 && HasNoExtension(image))
            image += ".gif";

        return IncomingPlayerPropertyUpdate.String(PlayerPropertyId.HorseGif, image);
    }

    private static IncomingPlayerPropertyUpdate ReadCurrentChat(GraalBinaryReader reader)
    {
        var length = reader.ReadGChar();
        var message = Encoding.ASCII.GetString(reader.ReadBytes(Math.Min((int)length, 223)));
        return IncomingPlayerPropertyUpdate.String(PlayerPropertyId.CurrentChat, message);
    }

    private static IncomingPlayerPropertyUpdate ReadSwordPower(
        GraalBinaryReader reader,
        ClientVersionId clientVersion)
    {
        var power = reader.ReadGChar();
        if (power <= 4)
            return IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.SwordPower, power);

        var length = reader.ReadGChar();
        var image = length == 0
            ? string.Empty
            : Encoding.ASCII.GetString(reader.ReadBytes(length));
        if (image.Length != 0 && clientVersion < ClientVersionId.Client21 && HasNoExtension(image))
            image += ".gif";

        return new IncomingPlayerPropertyUpdate(PlayerPropertyId.SwordPower, GCharValue: power, StringValue: image);
    }

    private static IncomingPlayerPropertyUpdate ReadShieldPower(
        GraalBinaryReader reader,
        ClientVersionId clientVersion)
    {
        var power = reader.ReadGChar();
        if (power <= 3)
            return IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.ShieldPower, power);

        if (reader.BytesLeft == 0)
            return IncomingPlayerPropertyUpdate.NoValue(PlayerPropertyId.ShieldPower);

        var decodedPower = power - 10;
        if (decodedPower < 0)
            return IncomingPlayerPropertyUpdate.NoValue(PlayerPropertyId.ShieldPower);

        var length = reader.ReadGChar();
        var image = length == 0
            ? string.Empty
            : Encoding.ASCII.GetString(reader.ReadBytes(length));
        if (image.Length != 0 && clientVersion < ClientVersionId.Client21 && HasNoExtension(image))
            image += ".gif";

        return new IncomingPlayerPropertyUpdate(PlayerPropertyId.ShieldPower, GCharValue: power, StringValue: image);
    }

    private static bool HasNoExtension(string value)
    {
        var slash = Math.Max(value.LastIndexOf('/'), value.LastIndexOf('\\'));
        var dot = value.LastIndexOf('.');
        return dot <= slash;
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
        bool appendNewline = false,
        ClientVersionId senderClientVersion = ClientVersionId.Client21,
        IncomingPlayerPropsForwardingState? state = null)
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

                case PlayerPropertyId.MaxPower:
                    WriteProperty(levelBuff, PlayerPropertyId.CurrentPower, writer => writer.WriteGChar((byte)(update.GCharValue.GetValueOrDefault() * 2)));
                    break;

                case PlayerPropertyId.CurrentPower:
                    if (state?.CurrentPowerRaw is { } currentPowerRaw)
                        WriteProperty(levelBuff, PlayerPropertyId.CurrentPower, writer => writer.WriteGChar(currentPowerRaw));
                    break;

                case PlayerPropertyId.Sprite:
                    WriteProperty(levelBuff, PlayerPropertyId.Sprite, writer => writer.WriteGChar(update.GCharValue.GetValueOrDefault()));
                    break;

                case PlayerPropertyId.SwordPower:
                    WriteProperty(levelBuff, PlayerPropertyId.SwordPower, writer => WritePowerImage(writer, update.GCharValue.GetValueOrDefault(), 30, update.StringValue ?? string.Empty));
                    break;

                case PlayerPropertyId.ShieldPower:
                    WriteProperty(levelBuff, PlayerPropertyId.ShieldPower, writer => WritePowerImage(writer, update.GCharValue.GetValueOrDefault(), 10, update.StringValue ?? string.Empty));
                    break;

                case PlayerPropertyId.CurrentLevel:
                    WriteProperty(levelBuff, PlayerPropertyId.CurrentLevel, writer => WriteGCharString(writer, state?.CurrentLevelName ?? update.StringValue ?? string.Empty));
                    break;

                case PlayerPropertyId.Gani:
                    WriteProperty(levelBuff, PlayerPropertyId.Gani, writer => WriteGani(writer, update, senderClientVersion));
                    break;

                case PlayerPropertyId.BodyImage:
                    WriteProperty(levelBuff, PlayerPropertyId.BodyImage, writer => WriteGCharString(writer, update.StringValue ?? string.Empty));
                    break;

                case PlayerPropertyId.HeadGif:
                    WriteProperty(levelBuff, PlayerPropertyId.HeadGif, writer => WriteHeadImage(writer, update.StringValue ?? string.Empty));
                    break;

                case PlayerPropertyId.HorseGif:
                    WriteProperty(levelBuff, PlayerPropertyId.HorseGif, writer => WriteGCharString(writer, update.StringValue ?? string.Empty));
                    break;

                case PlayerPropertyId.CurrentChat:
                    WriteProperty(levelBuff, PlayerPropertyId.CurrentChat, writer => WriteGCharString(writer, update.StringValue ?? string.Empty));
                    break;

                case PlayerPropertyId.AttachNpc:
                    WriteProperty(levelBuff, PlayerPropertyId.AttachNpc, writer =>
                    {
                        writer.WriteGChar(0);
                        writer.WriteGInt(GetUnsignedInt(update));
                    });
                    break;

                case PlayerPropertyId.CarrySprite:
                    WriteProperty(levelBuff, PlayerPropertyId.CarrySprite, writer => writer.WriteGChar(update.GCharValue.GetValueOrDefault()));
                    break;

                case PlayerPropertyId.ApCounter:
                    WriteProperty(levelBuff, PlayerPropertyId.ApCounter, writer => writer.WriteGShort((ushort)(update.GShortValue.GetValueOrDefault() + 1)));
                    break;

                case PlayerPropertyId.Alignment:
                    WriteProperty(levelBuff, PlayerPropertyId.Alignment, writer => writer.WriteGChar(Math.Min(update.GCharValue.GetValueOrDefault(), (byte)100)));
                    break;

                case PlayerPropertyId.UdpPort:
                    WriteProperty(levelBuff, PlayerPropertyId.UdpPort, writer => writer.WriteGInt(unchecked((uint)update.GIntValue.GetValueOrDefault())));
                    break;

                case PlayerPropertyId.PlayerStatusMessage:
                    WriteProperty(levelBuff, PlayerPropertyId.PlayerStatusMessage, writer => writer.WriteGChar(update.GCharValue.GetValueOrDefault()));
                    break;

                case PlayerPropertyId.AccountName:
                    if (state?.AccountName is { } accountName)
                        WriteProperty(levelBuff, PlayerPropertyId.AccountName, writer => WriteGCharString(writer, accountName));
                    break;

                case PlayerPropertyId.IpAddress:
                    if (state?.AccountIp is { } accountIp)
                        WriteProperty(levelBuff, PlayerPropertyId.IpAddress, writer => writer.WriteGInt5(accountIp));
                    break;

                case PlayerPropertyId.CommunityName:
                    if (state?.CommunityName is { } communityName)
                        WriteProperty(levelBuff, PlayerPropertyId.CommunityName, writer => WriteGCharString(writer, communityName));
                    break;

                case PlayerPropertyId.Rating:
                    if (state?.EloRating is { } eloRating && state?.EloDeviation is { } eloDeviation)
                    {
                        WriteProperty(levelBuff, PlayerPropertyId.Rating, writer =>
                            writer.WriteGInt((uint)(((eloRating & 0xFFF) << 9) | (eloDeviation & 0x1FF))));
                    }
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

    private static void WriteGani(
        GraalBinaryWriter writer,
        IncomingPlayerPropertyUpdate update,
        ClientVersionId clientVersion)
    {
        if (clientVersion >= ClientVersionId.Client21)
        {
            WriteGCharString(writer, update.StringValue ?? string.Empty);
            return;
        }

        var image = update.StringValue ?? string.Empty;
        if (image.Length > 0)
        {
            writer.WriteGChar((byte)(10 + image.Length));
            writer.WriteBytes(Encoding.ASCII.GetBytes(image));
            return;
        }

        writer.WriteGChar(update.GCharValue.GetValueOrDefault());
    }

    private static void WriteHeadImage(GraalBinaryWriter writer, string value)
    {
        writer.WriteGChar((byte)(value.Length + 100));
        writer.WriteBytes(Encoding.ASCII.GetBytes(value));
    }

    private static void WritePowerImage(GraalBinaryWriter writer, byte power, byte offset, string image)
    {
        writer.WriteGChar((byte)(power + offset));
        WriteGCharString(writer, image);
    }

    private static bool IsGaniAttributeProperty(PlayerPropertyId propertyId) =>
        propertyId is >= PlayerPropertyId.GAttrib1 and <= PlayerPropertyId.GAttrib5
            or >= PlayerPropertyId.GAttrib6 and <= PlayerPropertyId.GAttrib9
            or >= PlayerPropertyId.GAttrib10 and <= PlayerPropertyId.GAttrib30;

    private static uint GetUnsignedInt(IncomingPlayerPropertyUpdate update) =>
        update.GUIntValue ?? unchecked((uint)update.GIntValue.GetValueOrDefault());
}

public sealed record IncomingPlayerPropsForwardingState(
    byte CurrentPowerRaw,
    string? CurrentLevelName = null,
    string? AccountName = null,
    uint? AccountIp = null,
    string? CommunityName = null,
    int? EloRating = null,
    int? EloDeviation = null);

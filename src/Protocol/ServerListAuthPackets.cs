using System.Text;

namespace GServ.Protocol;

public sealed record ServerListVerifyAccount2Response(
    string AccountName,
    ushort PlayerId,
    PlayerSessionType Type,
    string Message)
{
    public bool IsSuccess => Message == "SUCCESS";
}

public static class ServerListAuthPackets
{
    public static byte[] RegisterV3(string version)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToListServerPacketId.RegisterV3);
        writer.WriteBytes(Encoding.ASCII.GetBytes(version));
        return writer.ToArray();
    }

    public static byte[] ServerHqPass(string password)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToListServerPacketId.ServerHqPass);
        writer.WriteBytes(Encoding.ASCII.GetBytes(password));
        return writer.ToArray();
    }

    public static byte[] ServerHqLevel(bool onlyStaff, int configuredLevel)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToListServerPacketId.ServerHqLevel);
        writer.WriteGChar((byte)(onlyStaff ? 0 : configuredLevel));
        return writer.ToArray();
    }

    public static byte[] NewServer(
        string name,
        string description,
        string language,
        string version,
        string url,
        string ip,
        string port,
        string localIp)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToListServerPacketId.NewServer);
        WriteGCharString(writer, name);
        WriteGCharString(writer, description);
        WriteGCharString(writer, language);
        WriteGCharString(writer, version);
        WriteGCharString(writer, url);
        WriteGCharString(writer, ip);
        WriteGCharString(writer, port);
        WriteGCharString(writer, localIp);
        return writer.ToArray();
    }

    public static byte[] AllowedVersionsText(IReadOnlyList<string> allowedVersions)
    {
        var versionNames = string.Join(",", allowedVersions.Select(GTokenize));
        return SendText($"Listserver,settings,allowedversions,{versionNames}");
    }

    public static byte[] SendText(string data)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToListServerPacketId.SendText);
        writer.WriteBytes(Encoding.ASCII.GetBytes(data));
        return writer.ToArray();
    }

    public static byte[] RequestListTextForPlayer(ushort playerId, string data)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToListServerPacketId.RequestList);
        writer.WriteGShort(playerId);
        writer.WriteBytes(Encoding.ASCII.GetBytes(data));
        return writer.ToArray();
    }

    public static byte[] Ping()
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToListServerPacketId.Ping);
        return writer.ToArray();
    }

    public static byte[] SetPlayers()
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToListServerPacketId.PlayerSet);
        return writer.ToArray();
    }

    public static byte[] VerifyAccount2Request(
        string accountName,
        string password,
        ushort playerId,
        PlayerSessionType type,
        string identity)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToListServerPacketId.VerifyAccount2);
        WriteGCharString(writer, accountName);
        WriteGCharString(writer, password);
        writer.WriteGShort(playerId);
        writer.WriteGChar((byte)type);
        writer.WriteGShort((ushort)Encoding.ASCII.GetByteCount(identity));
        writer.WriteBytes(Encoding.ASCII.GetBytes(identity));
        return writer.ToArray();
    }

    public static ServerListVerifyAccount2Response ParseVerifyAccount2Response(ReadOnlySpan<byte> payloadWithoutPacketId)
    {
        var reader = new GraalBinaryReader(payloadWithoutPacketId);
        var account = ReadGCharString(reader);
        var id = reader.ReadGShort();
        var type = (PlayerSessionType)reader.ReadGChar();
        var message = Encoding.ASCII.GetString(reader.ReadBytes(reader.BytesLeft));
        return new ServerListVerifyAccount2Response(account, id, type, message);
    }

    private static void WriteGCharString(GraalBinaryWriter writer, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        writer.WriteGChar((byte)bytes.Length);
        writer.WriteBytes(bytes);
    }

    private static string ReadGCharString(GraalBinaryReader reader) =>
        Encoding.ASCII.GetString(reader.ReadBytes(reader.ReadGChar()));

    private static string GTokenize(string value)
    {
        var lines = value.EndsWith('\n') ? value.Split('\n') : (value + "\n").Split('\n');
        var tokens = new List<string>();
        foreach (var raw in lines.Take(lines.Length - 1))
        {
            var temp = raw.Replace("\r", string.Empty, StringComparison.Ordinal);
            var complex = temp.StartsWith('"') ||
                          temp.Any(static c => c < 33 || c > 126 || c == ',' || c == '/') ||
                          temp.Trim().Length == 0;

            if (complex)
            {
                temp = temp
                    .Replace("\\", "\\\\", StringComparison.Ordinal)
                    .Replace("\"", "\"\"", StringComparison.Ordinal);
                tokens.Add($"\"{temp}\"");
            }
            else
            {
                tokens.Add(temp);
            }
        }

        return string.Join(",", tokens);
    }
}

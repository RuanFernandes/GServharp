using System.Text;

namespace Preagonal.GServer.Protocol;

public static class EntityPackets
{
    public static byte[] ItemAdd(byte encodedX, byte encodedY, byte itemType)
    {
        var writer = NewPacket(ServerToPlayerPacketId.ItemAdd);
        writer.WriteGChar(encodedX);
        writer.WriteGChar(encodedY);
        writer.WriteGChar(itemType);
        return WithNewline(writer);
    }

    public static byte[] ItemDelete(byte encodedX, byte encodedY)
    {
        var writer = NewPacket(ServerToPlayerPacketId.ItemDelete);
        writer.WriteGChar(encodedX);
        writer.WriteGChar(encodedY);
        return WithNewline(writer);
    }

    public static byte[] ItemDeleteFromLevelCoordinates(float x, float y) =>
        ItemDelete((byte)(x * 2), (byte)(y * 2));

    public static byte[] HorseAdd(float x, float y, byte direction, byte bushes, string image)
    {
        var writer = NewPacket(ServerToPlayerPacketId.HorseAdd);
        writer.WriteByte((byte)(x * 2));
        writer.WriteGChar((byte)(y * 2));
        writer.WriteGChar((byte)((bushes << 2) | (direction & 0x03)));
        writer.WriteBytes(Encoding.ASCII.GetBytes(image));
        return WithNewline(writer);
    }

    public static byte[] HorseDelete(float x, float y)
    {
        var writer = NewPacket(ServerToPlayerPacketId.HorseDelete);
        writer.WriteGChar((byte)(x * 2));
        writer.WriteGChar((byte)(y * 2));
        return WithNewline(writer);
    }

    public static byte[] DefaultWeapon(byte itemType)
    {
        var writer = NewPacket(ServerToPlayerPacketId.DefaultWeapon);
        writer.WriteGChar(itemType);
        return WithNewline(writer);
    }

    public static byte[] NpcWeaponAdd(string name, string image, string formattedClientGs1)
    {
        var writer = NewPacket(ServerToPlayerPacketId.NpcWeaponAdd);
        WriteGCharString(writer, name);
        writer.WriteGChar(0);
        WriteGCharString(writer, image);
        writer.WriteGChar(1);
        writer.WriteGShort((ushort)Encoding.Latin1.GetByteCount(formattedClientGs1));
        writer.WriteBytes(Encoding.Latin1.GetBytes(formattedClientGs1));
        return WithNewline(writer);
    }

    public static byte[] NpcWeaponDelete(string name)
    {
        var writer = NewPacket(ServerToPlayerPacketId.NpcWeaponDelete);
        writer.WriteBytes(Encoding.ASCII.GetBytes(name));
        return WithNewline(writer);
    }

    public static byte[] NpcWeaponScriptRawData(ReadOnlySpan<byte> bytecode)
    {
        var writer = NewPacket(ServerToPlayerPacketId.RawData);
        writer.WriteGInt((uint)bytecode.Length);
        writer.WriteByte((byte)'\n');
        writer.WriteGChar((byte)ServerToPlayerPacketId.NpcWeaponScript);
        writer.WriteBytes(bytecode);
        return writer.ToArray();
    }

    public static UpdateGaniRequest ParseUpdateGani(ReadOnlySpan<byte> packet)
    {
        var reader = new GraalBinaryReader(packet);
        var opcode = (PlayerToServerPacketId)reader.ReadGChar();
        if (opcode != PlayerToServerPacketId.UpdateGani)
            throw new InvalidDataException($"Expected {nameof(PlayerToServerPacketId.UpdateGani)} packet.");

        var checksum = reader.ReadGInt5();
        var gani = Encoding.ASCII.GetString(reader.ReadBytes(reader.BytesLeft));
        return new UpdateGaniRequest(checksum, gani);
    }

    public static bool ShouldSendGaniScript(ReadOnlySpan<byte> bytecode, uint clientChecksum) =>
        Crc32.Compute(bytecode) != clientChecksum;

    public static byte[] GaniScriptRawData(string ganiName, ReadOnlySpan<byte> bytecode)
    {
        var gani = StripGaniExtension(ganiName);
        if (gani.Length == 0 || bytecode.Length == 0)
            return [];

        var writer = NewPacket(ServerToPlayerPacketId.RawData);
        writer.WriteGInt((uint)(bytecode.Length + Encoding.ASCII.GetByteCount(gani) + 1));
        writer.WriteByte((byte)'\n');
        writer.WriteGChar((byte)ServerToPlayerPacketId.GaniScript);
        WriteGCharString(writer, gani);
        writer.WriteBytes(bytecode);
        return writer.ToArray();
    }

    public static byte[] LoadGaniSetBackTo(string gani, string setBackTo)
    {
        var writer = NewPacket(ServerToPlayerPacketId.LoadGani);
        WriteGCharString(writer, gani);
        writer.WriteByte((byte)'"');
        writer.WriteBytes(Encoding.ASCII.GetBytes("SETBACKTO "));
        writer.WriteBytes(Encoding.ASCII.GetBytes(setBackTo));
        writer.WriteByte((byte)'"');
        return WithNewline(writer);
    }

    public static byte[] MissingClassScriptHeader(string className)
    {
        var header = string.Join(
            ",",
            [
                GTokenize("class"),
                GTokenize(className),
                GTokenize("1"),
                GTokenize(GInt5String(0) + GInt5String(0)),
                GTokenize(GInt5String(0))
            ]);

        var writer = NewPacket(ServerToPlayerPacketId.NpcWeaponScript);
        writer.WriteRawShort((ushort)Encoding.ASCII.GetByteCount(header));
        writer.WriteBytes(Encoding.ASCII.GetBytes(header));
        return WithNewline(writer);
    }

    public static byte[] NpcDelete(uint npcId)
    {
        var writer = NewPacket(ServerToPlayerPacketId.NpcDelete);
        writer.WriteGInt(npcId);
        return WithNewline(writer);
    }

    public static byte[] NpcProps(uint npcId, ReadOnlySpan<byte> props)
    {
        var writer = NewPacket(ServerToPlayerPacketId.NpcProps);
        writer.WriteGInt(npcId);
        writer.WriteBytes(props);
        return WithNewline(writer);
    }

    public static byte[] NpcDelete2(string levelName, uint npcId)
    {
        var writer = NewPacket(ServerToPlayerPacketId.NpcDelete2);
        writer.WriteBytes(Encoding.ASCII.GetBytes(levelName));
        writer.WriteGInt(npcId);
        return WithNewline(writer);
    }

    private static GraalBinaryWriter NewPacket(ServerToPlayerPacketId packetId)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)packetId);
        return writer;
    }

    private static void WriteGCharString(GraalBinaryWriter writer, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        writer.WriteGChar((byte)bytes.Length);
        writer.WriteBytes(bytes);
    }

    private static string StripGaniExtension(string ganiName) =>
        ganiName.EndsWith(".gani", StringComparison.Ordinal)
            ? ganiName[..^5]
            : ganiName;

    private static string GInt5String(uint value)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGInt5(value);
        return Encoding.ASCII.GetString(writer.ToArray());
    }

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

    private static byte[] WithNewline(GraalBinaryWriter writer)
    {
        writer.WriteByte((byte)'\n');
        return writer.ToArray();
    }
}

public sealed record UpdateGaniRequest(uint Checksum, string Gani)
{
    public string GaniFile => Gani + ".gani";
}

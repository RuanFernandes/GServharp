using System.Text;

namespace GServ.Protocol;

public sealed record RcFileBrowserEntry(string Name, string Rights, uint Size, uint ModifiedTime);

public static class RcNcPackets
{
    public static byte[] RcChat(string message) =>
        PacketWithAsciiPayload(ServerToPlayerPacketId.RcChat, message);

    public static byte[] RcMaxUploadFileSize(uint bytes)
    {
        var writer = NewServerPacket(ServerToPlayerPacketId.RcMaxUploadFileSize);
        writer.WriteGInt5(bytes);
        return WithTrailingNewline(writer);
    }

    public static byte[] FileBrowserMessage(string message) =>
        PacketWithAsciiPayload(ServerToPlayerPacketId.RcFileBrowserMessage, message);

    public static byte[] FileBrowserDirList(string tokenizedFolders) =>
        PacketWithAsciiPayload(ServerToPlayerPacketId.RcFileBrowserDirList, tokenizedFolders);

    public static byte[] FileBrowserDir(string folder, IReadOnlyList<RcFileBrowserEntry> entries)
    {
        var writer = NewServerPacket(ServerToPlayerPacketId.RcFileBrowserDir);
        WriteGCharString(writer, folder);

        foreach (var entry in entries)
        {
            writer.WriteByte((byte)' ');

            var entryWriter = new GraalBinaryWriter();
            WriteGCharString(entryWriter, entry.Name);
            WriteGCharString(entryWriter, entry.Rights);
            entryWriter.WriteGInt5(entry.Size);
            entryWriter.WriteGInt5(entry.ModifiedTime);
            var entryBytes = entryWriter.ToArray();

            writer.WriteGChar((byte)entryBytes.Length);
            writer.WriteBytes(entryBytes);
        }

        return WithTrailingNewline(writer);
    }

    public static byte[] NcWeaponList(IReadOnlyList<string> weaponNames)
    {
        var writer = NewServerPacket(ServerToPlayerPacketId.NcWeaponListGet);
        foreach (var weaponName in weaponNames)
        {
            WriteGCharString(writer, weaponName);
        }

        return WithTrailingNewline(writer);
    }

    public static byte[] NcWeaponGet(string weaponName, string imageName, string script)
    {
        var writer = NewServerPacket(ServerToPlayerPacketId.NcWeaponGet);
        WriteGCharString(writer, weaponName);
        WriteGCharString(writer, imageName);
        writer.WriteBytes(Encoding.Latin1.GetBytes(script.Replace('\n', '\u00a7')));
        return WithTrailingNewline(writer);
    }

    public static byte[] LegacyNcWeaponGet(string weaponName, string imageName, string script)
    {
        var formattedScript = Encoding.Latin1.GetBytes(script.Replace('\n', '\u00a7'));
        var writer = NewServerPacket(ServerToPlayerPacketId.NpcWeaponAdd);
        WriteGCharString(writer, weaponName);
        writer.WriteGChar(0);
        WriteGCharString(writer, imageName);
        writer.WriteGChar(1);
        writer.WriteGShort((ushort)formattedScript.Length);
        writer.WriteBytes(formattedScript);
        return WithTrailingNewline(writer);
    }

    public static byte[] NpcServerAddress(ushort npcServerId, string ip, int port)
    {
        var writer = NewServerPacket(ServerToPlayerPacketId.NpcServerAddress);
        writer.WriteGShort(npcServerId);
        writer.WriteBytes(Encoding.ASCII.GetBytes($"{ip},{port}"));
        return WithTrailingNewline(writer);
    }

    private static byte[] PacketWithAsciiPayload(ServerToPlayerPacketId packetId, string payload)
    {
        var writer = NewServerPacket(packetId);
        writer.WriteBytes(Encoding.ASCII.GetBytes(payload));
        return WithTrailingNewline(writer);
    }

    private static GraalBinaryWriter NewServerPacket(ServerToPlayerPacketId packetId)
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

    private static byte[] WithTrailingNewline(GraalBinaryWriter writer)
    {
        writer.WriteByte((byte)'\n');
        return writer.ToArray();
    }
}

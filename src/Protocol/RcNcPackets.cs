using System.Text;

namespace Preagonal.GServer.Protocol;

public sealed record RcFileBrowserEntry(string Name, string Rights, uint Size, uint ModifiedTime);

public static class RcNcPackets
{
    public static byte[] ClearWeapons() =>
        BlankPacket(ServerToPlayerPacketId.ClearWeapons);

    public static byte[] Unknown190() =>
        BlankPacket(ServerToPlayerPacketId.ServerListConnected);

    public static byte[] RcChat(string message) =>
        PacketWithAsciiPayload(ServerToPlayerPacketId.RcChat, message);

    public static byte[] StaffGuilds(string guilds)
    {
        var writer = NewServerPacket(ServerToPlayerPacketId.StaffGuilds);
        var wroteGuild = false;
        foreach (var guild in guilds.Split(',', StringSplitOptions.None))
        {
            var trimmed = guild.Trim();
            if (trimmed.Length == 0)
                continue;

            if (wroteGuild)
                writer.WriteByte((byte)',');
            writer.WriteByte((byte)'"');
            writer.WriteBytes(Encoding.ASCII.GetBytes(trimmed.Replace("\"", "\"\"", StringComparison.Ordinal)));
            writer.WriteByte((byte)'"');
            wroteGuild = true;
        }

        return WithTrailingNewline(writer);
    }

    public static byte[] StatusList(string statuses)
    {
        var values = statuses
            .Split(',', StringSplitOptions.None)
            .Select(static status => status.Trim())
            .Where(static status => status.Length != 0);
        return PacketWithAsciiPayload(ServerToPlayerPacketId.StatusList, string.Join(",", values));
    }

    public static byte[] RcMaxUploadFileSize(uint bytes)
    {
        var writer = NewServerPacket(ServerToPlayerPacketId.RcMaxUploadFileSize);
        writer.WriteGInt5(bytes);
        return WithTrailingNewline(writer);
    }

    public static byte[] FileBrowserMessage(string message) =>
        PacketWithAsciiPayload(ServerToPlayerPacketId.RcFileBrowserMessage, message);

    public static byte[] RcAdminMessage(string message) =>
        PacketWithAsciiPayload(ServerToPlayerPacketId.RcAdminMessage, message);

    public static byte[] ServerOptionsGet(string optionsText) =>
        PacketWithAsciiPayload(ServerToPlayerPacketId.RcServerOptionsGet, GTokenize(optionsText));

    public static byte[] FolderConfigGet(string folderConfigText) =>
        PacketWithAsciiPayload(ServerToPlayerPacketId.RcFolderConfigGet, GTokenize(folderConfigText));

    public static byte[] ServerFlagsGet(IReadOnlyList<KeyValuePair<string, string>> flags)
    {
        var writer = NewServerPacket(ServerToPlayerPacketId.RcServerFlagsGet);
        writer.WriteGShort((ushort)flags.Count);
        foreach (var flag in flags)
        {
            var value = flag.Value.Length == 0 ? flag.Key : $"{flag.Key}={flag.Value}";
            WriteGCharString(writer, value);
        }

        return WithTrailingNewline(writer);
    }

    public static byte[] AccountListGet(IEnumerable<string> accountNames)
    {
        var writer = NewServerPacket(ServerToPlayerPacketId.RcAccountListGet);
        foreach (var accountName in accountNames)
            WriteGCharString(writer, accountName);

        return WithTrailingNewline(writer);
    }

    public static byte[] AccountGet(AccountView account)
    {
        var writer = NewServerPacket(ServerToPlayerPacketId.RcAccountGet);
        WriteGCharString(writer, account.AccountName);
        writer.WriteGChar(0);
        WriteGCharString(writer, account.Email);
        writer.WriteGChar((byte)(account.IsBanned ? 1 : 0));
        writer.WriteGChar((byte)(account.IsLoadOnly ? 1 : 0));
        writer.WriteGChar(0);
        WriteGCharString(writer, "main");
        WriteGCharString(writer, account.BanLength);
        WriteGCharString(writer, account.BanReason);
        return WithTrailingNewline(writer);
    }

    public static byte[] PlayerRightsGet(AccountRightsView account)
    {
        var writer = NewServerPacket(ServerToPlayerPacketId.RcPlayerRightsGet);
        WriteGCharString(writer, account.AccountName);
        writer.WriteGInt5(unchecked((uint)account.AdminRights));
        WriteGCharString(writer, account.AdminIp);
        var folders = GTokenize(string.Join('\n', account.FolderRights) + "\n");
        writer.WriteGShort((ushort)folders.Length);
        writer.WriteBytes(Encoding.ASCII.GetBytes(folders));
        return WithTrailingNewline(writer);
    }

    public static byte[] PlayerCommentsGet(string accountName, string comments)
    {
        var writer = NewServerPacket(ServerToPlayerPacketId.RcPlayerCommentsGet);
        WriteGCharString(writer, accountName);
        writer.WriteBytes(Encoding.ASCII.GetBytes(comments));
        return WithTrailingNewline(writer);
    }

    public static byte[] PlayerBanGet(string accountName, bool isBanned, string reason)
    {
        var writer = NewServerPacket(ServerToPlayerPacketId.RcPlayerBanGet);
        WriteGCharString(writer, accountName);
        writer.WriteGChar((byte)(isBanned ? 1 : 0));
        writer.WriteBytes(Encoding.ASCII.GetBytes(reason));
        return WithTrailingNewline(writer);
    }

    public static byte[] PlayerPropsGet(ushort playerId, byte[] props)
    {
        var writer = NewServerPacket(ServerToPlayerPacketId.RcPlayerPropertiesGet);
        writer.WriteGShort(playerId);
        writer.WriteBytes(props);
        return WithTrailingNewline(writer);
    }

    public static byte[] AddPlayer(
        ushort playerId,
        string accountName,
        string levelName,
        byte statusMessage,
        string nickname,
        string communityName)
    {
        var writer = NewServerPacket(ServerToPlayerPacketId.AddPlayer);
        writer.WriteGShort(playerId);
        WriteGCharString(writer, accountName);
        writer.WriteGChar((byte)PlayerPropertyId.CurrentLevel);
        WriteGCharString(writer, string.IsNullOrEmpty(levelName) ? " " : levelName);
        writer.WriteGChar((byte)PlayerPropertyId.PlayerStatusMessage);
        writer.WriteGChar(statusMessage);
        writer.WriteGChar((byte)PlayerPropertyId.Nickname);
        WriteGCharString(writer, nickname);
        writer.WriteGChar((byte)PlayerPropertyId.CommunityName);
        WriteGCharString(writer, communityName);
        return WithTrailingNewline(writer);
    }

    public static byte[] FileBrowserDirList(string folders) =>
        PacketWithAsciiPayload(ServerToPlayerPacketId.RcFileBrowserDirList, GTokenize(folders));

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

    public static byte[] NcLevelList(IReadOnlyList<string> levelNames) =>
        PacketWithAsciiPayload(ServerToPlayerPacketId.NcLevelList, GTokenize(string.Join('\n', levelNames) + "\n"));

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

    public static byte[] NcClassGet(string className, string source)
    {
        var writer = NewServerPacket(ServerToPlayerPacketId.NcClassGet);
        WriteGCharString(writer, className);
        writer.WriteBytes(Encoding.ASCII.GetBytes(GTokenize(source)));
        return WithTrailingNewline(writer);
    }

    public static byte[] NcClassAdd(string className) =>
        PacketWithAsciiPayload(ServerToPlayerPacketId.NcClassAdd, className);

    public static byte[] NcClassDelete(string className) =>
        PacketWithAsciiPayload(ServerToPlayerPacketId.NcClassDelete, className);

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

    private static byte[] BlankPacket(ServerToPlayerPacketId packetId) =>
        WithTrailingNewline(NewServerPacket(packetId));

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

    private static byte[] WithTrailingNewline(GraalBinaryWriter writer)
    {
        writer.WriteByte((byte)'\n');
        return writer.ToArray();
    }
}

public sealed record AccountView(
    string AccountName,
    string Email,
    bool IsBanned,
    bool IsLoadOnly,
    string BanLength,
    string BanReason);

public sealed record AccountRightsView(
    string AccountName,
    int AdminRights,
    string AdminIp,
    IReadOnlyList<string> FolderRights);

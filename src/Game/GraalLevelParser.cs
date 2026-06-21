using System.Globalization;
using System.Text;

namespace Preagonal.GServer.Game;

public static class GraalLevelParser
{
    private const int TileCount = 64 * 64;

    public static NwLevelParseResult Parse(byte[] content, Func<string, bool>? linkTargetExists = null)
    {
        if (content.Length < 8)
            return NwLevelParseResult.Failed;

        var reader = new LegacyGraalLevelReader(content);
        var version = reader.ReadChars(8);
        var formatVersion = GetFormatVersion(version);
        if (formatVersion < 0)
            return NwLevelParseResult.Failed;

        var snapshot = new NwLevelSnapshot(version, [], [], [], [], []);
        ReadTiles(reader, snapshot, formatVersion > 0 ? 13 : 12);

        var links = ReadLinks(reader, linkTargetExists);
        var baddies = ReadBaddies(reader);
        var npcs = ReadNpcs(reader);
        var chests = formatVersion > 0
            ? ReadChests(reader)
            : [];
        var signs = ReadSigns(reader);

        var parsed = new NwLevelSnapshot(version, links, signs, npcs, baddies, chests);
        Array.Copy(snapshot.Layers[0], parsed.Layers[0], TileCount);
        return new NwLevelParseResult(true, parsed);
    }

    private static int GetFormatVersion(string version) =>
        version switch
        {
            "GR-V1.00" => 0,
            "GR-V1.01" => 1,
            "GR-V1.02" => 2,
            "GR-V1.03" => 3,
            _ => -1,
        };

    private static void ReadTiles(LegacyGraalLevelReader reader, NwLevelSnapshot snapshot, int bits)
    {
        var read = 0;
        uint buffer = 0;
        var tiles = new[] { -1, -1 };
        var boardIndex = 0;
        var count = 1;
        var doubleMode = false;
        var controlMask = bits == 12 ? 0x800 : 0x1000;
        var codeMask = bits == 12 ? 0xfff : 0x1fff;

        while (boardIndex < TileCount && reader.BytesLeft != 0)
        {
            while (read < bits && reader.BytesLeft != 0)
            {
                buffer += (uint)reader.ReadByte() << read;
                read += 8;
            }

            if (read < bits)
                break;

            var code = (int)(buffer & codeMask);
            buffer >>= bits;
            read -= bits;

            if ((code & controlMask) != 0)
            {
                if ((code & 0x100) != 0)
                    doubleMode = true;

                count = code & 0xff;
                continue;
            }

            if (count == 1)
            {
                SetTile(snapshot, boardIndex++, code);
                continue;
            }

            if (doubleMode)
            {
                if (tiles[0] == -1)
                {
                    tiles[0] = code;
                    continue;
                }

                tiles[1] = code;
                for (var i = 0; i < count && boardIndex < TileCount - 1; ++i)
                {
                    SetTile(snapshot, boardIndex++, tiles[0]);
                    SetTile(snapshot, boardIndex++, tiles[1]);
                }

                tiles[0] = tiles[1] = -1;
                doubleMode = false;
                count = 1;
            }
            else
            {
                for (var i = 0; i < count && boardIndex < TileCount; ++i)
                    SetTile(snapshot, boardIndex++, code);
                count = 1;
            }
        }
    }

    private static void SetTile(NwLevelSnapshot snapshot, int boardIndex, int tile)
    {
        snapshot.SetTile(0, boardIndex % 64, boardIndex / 64, unchecked((ushort)tile));
    }

    private static IReadOnlyList<NwLevelLink> ReadLinks(
        LegacyGraalLevelReader reader,
        Func<string, bool>? linkTargetExists)
    {
        var links = new List<NwLevelLink>();
        while (reader.BytesLeft != 0)
        {
            var line = reader.ReadString('\n');
            if (line.Length == 0 || line == "#")
                break;

            var tokens = TokenizeBySpace(line);
            if (tokens.Count < 7)
                continue;

            var level = tokens[0];
            if (tokens.Count > 7)
            {
                level = string.Join(" ", tokens.Take(tokens.Count - 6));
            }

            if (linkTargetExists is null || !linkTargetExists(level))
                continue;

            var offset = tokens.Count - 7;
            links.Add(new NwLevelLink(
                level,
                Atoi(tokens[1 + offset]),
                Atoi(tokens[2 + offset]),
                Atoi(tokens[3 + offset]),
                Atoi(tokens[4 + offset]),
                tokens[5 + offset],
                tokens[6 + offset]));
        }

        return links;
    }

    private static IReadOnlyList<NwLevelBaddy> ReadBaddies(LegacyGraalLevelReader reader)
    {
        var baddies = new List<NwLevelBaddy>();
        while (reader.BytesLeft != 0)
        {
            var x = reader.ReadSignedByte();
            var y = reader.ReadSignedByte();
            var type = reader.ReadSignedByte();

            if (x == -1 && y == -1 && type == -1)
            {
                _ = reader.ReadString('\n');
                break;
            }

            var verses = reader.ReadString('\n').Split('\\', StringSplitOptions.None);
            baddies.Add(new NwLevelBaddy(x, y, type, verses));
        }

        return baddies;
    }

    private static IReadOnlyList<NwLevelNpc> ReadNpcs(LegacyGraalLevelReader reader)
    {
        var npcs = new List<NwLevelNpc>();
        while (reader.BytesLeft != 0)
        {
            var line = reader.ReadString('\n');
            if (line.Length == 0 || line == "#")
                break;

            var lineReader = new LegacyGraalLineReader(line);
            var x = lineReader.ReadGChar();
            var y = lineReader.ReadGChar();
            var image = lineReader.ReadString('#');
            var code = lineReader.ReadRest().Replace("\u00a7", "\n", StringComparison.Ordinal);

            npcs.Add(new NwLevelNpc(image, x, y, code));
        }

        return npcs;
    }

    private static IReadOnlyList<NwLevelChest> ReadChests(LegacyGraalLevelReader reader)
    {
        var chests = new List<NwLevelChest>();
        while (reader.BytesLeft != 0)
        {
            var line = reader.ReadString('\n');
            if (line.Length == 0 || line == "#")
                break;

            var lineReader = new LegacyGraalLineReader(line);
            var x = lineReader.ReadGChar();
            var y = lineReader.ReadGChar();
            var item = lineReader.ReadGChar();
            var signIndex = lineReader.ReadGChar();
            chests.Add(new NwLevelChest(x, y, (LevelItemType)item, signIndex));
        }

        return chests;
    }

    private static IReadOnlyList<NwLevelSign> ReadSigns(LegacyGraalLevelReader reader)
    {
        var signs = new List<NwLevelSign>();
        while (reader.BytesLeft != 0)
        {
            var line = reader.ReadString('\n');
            if (line.Length == 0)
                break;

            var lineReader = new LegacyGraalLineReader(line);
            var x = lineReader.ReadGChar();
            var y = lineReader.ReadGChar();
            signs.Add(new NwLevelSign(x, y, lineReader.ReadRest()));
        }

        return signs;
    }

    private static IReadOnlyList<string> TokenizeBySpace(string line) =>
        line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static int Atoi(string value)
    {
        _ = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed);
        return parsed;
    }

    private sealed class LegacyGraalLevelReader(byte[] buffer)
    {
        private int _position;

        public int BytesLeft => Math.Max(0, buffer.Length - _position);

        public byte ReadByte()
        {
            if (_position >= buffer.Length)
                return 0;

            return buffer[_position++];
        }

        public sbyte ReadSignedByte() => unchecked((sbyte)ReadByte());

        public string ReadChars(int count)
        {
            count = Math.Clamp(count, 0, BytesLeft);
            var value = Encoding.Latin1.GetString(buffer, _position, count);
            _position += count;
            return value;
        }

        public string ReadString(char delimiter)
        {
            var start = _position;
            while (_position < buffer.Length && buffer[_position] != delimiter)
                _position++;

            var value = Encoding.Latin1.GetString(buffer, start, _position - start);
            if (_position < buffer.Length && buffer[_position] == delimiter)
                _position++;

            return value;
        }
    }

    private sealed class LegacyGraalLineReader(string line)
    {
        private int _position;

        public int ReadGChar()
        {
            if (_position >= line.Length)
                return -32;

            return unchecked((sbyte)(byte)line[_position++]) - 32;
        }

        public string ReadString(char delimiter)
        {
            var start = _position;
            while (_position < line.Length && line[_position] != delimiter)
                _position++;

            var value = line[start.._position];
            if (_position < line.Length && line[_position] == delimiter)
                _position++;

            return value;
        }

        public string ReadRest()
        {
            var value = _position >= line.Length ? string.Empty : line[_position..];
            _position = line.Length;
            return value;
        }
    }
}

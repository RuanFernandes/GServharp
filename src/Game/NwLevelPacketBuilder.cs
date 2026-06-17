using GServ.Protocol;
using System.Globalization;
using System.Text;

namespace GServ.Game;

public static class NwLevelPacketBuilder
{
    private const string SignText =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz" +
        "0123456789!?-.,#>()#####\"####':/~&### <####;\n";
    private const string SignSymbols = "ABXYudlrhxyz#4.";
    private static readonly int[] CodeTableLengths = [1, 1, 1, 1, 1, 1, 1, 1, 2, 1, 1, 1, 2, 2, 1];
    private static readonly int[] CodeTableIndexes = [0, 1, 2, 3, 4, 5, 6, 7, 8, 10, 11, 12, 13, 15, 17];
    private static readonly int[] CodeTable = [91, 92, 93, 94, 77, 78, 79, 80, 74, 75, 71, 72, 73, 86, 86, 87, 88, 67];

    public static byte[] BuildBoardPacket(NwLevelSnapshot level)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.BoardPacket);
        WriteRawTileLayer(writer, level, 0);
        writer.WriteByte((byte)'\n');
        return writer.ToArray();
    }

    public static byte[] BuildLayerPacket(NwLevelSnapshot level, int layer)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.BoardLayer);
        writer.WriteByte((byte)layer);
        writer.WriteByte(0);
        writer.WriteByte(0);
        writer.WriteByte(64);
        writer.WriteByte(64);
        WriteRawTileLayer(writer, level, layer);
        writer.WriteByte((byte)'\n');
        return writer.ToArray();
    }

    public static byte[] BuildLinksPacket(NwLevelSnapshot level)
    {
        var writer = new GraalBinaryWriter();
        foreach (var link in level.Links)
        {
            writer.WriteGChar((byte)ServerToPlayerPacketId.LevelLink);
            writer.WriteBytes(Encoding.ASCII.GetBytes(BuildLinkString(link)));
            writer.WriteByte((byte)'\n');
        }

        return writer.ToArray();
    }

    public static byte[] BuildSignsPacket(NwLevelSnapshot level)
    {
        var writer = new GraalBinaryWriter();
        foreach (var sign in level.Signs)
        {
            writer.WriteGChar((byte)ServerToPlayerPacketId.LevelSign);
            writer.WriteGChar((byte)sign.X);
            writer.WriteGChar((byte)sign.Y);
            writer.WriteBytes(EncodeSign(sign.Text));
            writer.WriteByte((byte)'\n');
        }

        return writer.ToArray();
    }

    public static byte[] BuildChestPacket(
        NwLevelSnapshot level,
        string levelName,
        Func<string, bool> playerHasChest)
    {
        var writer = new GraalBinaryWriter();
        foreach (var chest in level.Chests)
        {
            var hasChest = playerHasChest(BuildChestKey(chest, levelName));

            writer.WriteGChar((byte)ServerToPlayerPacketId.LevelChest);
            writer.WriteGChar((byte)(hasChest ? 1 : 0));
            writer.WriteGChar((byte)chest.X);
            writer.WriteGChar((byte)chest.Y);

            if (!hasChest)
            {
                writer.WriteGChar((byte)chest.ItemType);
                writer.WriteGChar((byte)chest.SignIndex);
            }

            writer.WriteByte((byte)'\n');
        }

        return writer.ToArray();
    }

    private static void WriteRawTileLayer(GraalBinaryWriter writer, NwLevelSnapshot level, int layer)
    {
        level.Layers.TryGetValue(layer, out var tiles);
        tiles ??= new ushort[64 * 64];

        foreach (var tile in tiles)
        {
            writer.WriteByte((byte)(tile & 0xFF));
            writer.WriteByte((byte)(tile >> 8));
        }
    }

    private static string BuildLinkString(NwLevelLink link)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{link.NewLevel} {link.X} {link.Y} {link.Width} {link.Height} {link.NewX} {link.NewY}");
    }

    private static string BuildChestKey(NwLevelChest chest, string levelName)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{chest.X}:{chest.Y}:{levelName}");
    }

    private static byte[] EncodeSign(string text)
    {
        var output = new List<byte>();
        var offset = 0;
        while (offset < text.Length)
        {
            var newlineIndex = text.IndexOf('\n', offset);
            string line;
            if (newlineIndex < 0)
            {
                line = text[offset..];
                offset = text.Length;
            }
            else
            {
                line = text[offset..newlineIndex];
                offset = newlineIndex + 1;
            }

            EncodeSignCode(line + "\n", output);
        }

        return output.ToArray();
    }

    private static void EncodeSignCode(string text, ICollection<byte> output)
    {
        for (var i = 0; i < text.Length; i++)
        {
            var letter = text[i];
            if (letter == '#')
            {
                i++;
                if (i < text.Length)
                {
                    letter = text[i];
                    var symbolCode = SignSymbols.IndexOf(letter, StringComparison.Ordinal);
                    if (symbolCode != -1)
                    {
                        for (var ii = 0; ii < CodeTableLengths[symbolCode]; ii++)
                        {
                            output.Add(WriteGCharByte((byte)CodeTable[CodeTableIndexes[symbolCode] + ii]));
                        }

                        continue;
                    }

                    letter = text[--i];
                }
            }

            var code = SignText.IndexOf(letter, StringComparison.Ordinal);
            if (letter == '#')
            {
                code = 86;
            }

            if (code != -1)
            {
                output.Add(WriteGCharByte((byte)code));
            }
            else if (letter != '\r')
            {
                output.Add(WriteGCharByte(86));
                output.Add(WriteGCharByte(10));
                output.Add(WriteGCharByte(69));
                foreach (var digit in ((int)letter).ToString(CultureInfo.InvariantCulture))
                {
                    var digitCode = SignText.IndexOf(digit, StringComparison.Ordinal);
                    if (digitCode != -1)
                    {
                        output.Add(WriteGCharByte((byte)digitCode));
                    }
                }

                output.Add(WriteGCharByte(70));
            }
        }
    }

    private static byte WriteGCharByte(byte value)
    {
        return (byte)(Math.Min(value, (byte)223) + 32);
    }
}

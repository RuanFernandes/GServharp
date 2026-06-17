using System.Globalization;

namespace GServ.Game;

public sealed record NwLevelParseResult(bool Success, NwLevelSnapshot Level)
{
    public static NwLevelParseResult Failed { get; } = new(false, NwLevelSnapshot.Empty);
}

public sealed class NwLevelSnapshot
{
    public static NwLevelSnapshot Empty { get; } = new(string.Empty, [], [], [], [], []);

    private readonly Dictionary<int, ushort[]> _layers = [];

    public NwLevelSnapshot(
        string fileVersion,
        IEnumerable<NwLevelLink> links,
        IEnumerable<NwLevelSign> signs,
        IEnumerable<NwLevelNpc> npcs,
        IEnumerable<NwLevelBaddy> baddies,
        IEnumerable<NwLevelChest> chests)
    {
        FileVersion = fileVersion;
        Links = links.ToArray();
        Signs = signs.ToArray();
        Npcs = npcs.ToArray();
        Baddies = baddies.ToArray();
        Chests = chests.ToArray();
        _layers[0] = new ushort[64 * 64];
    }

    public string FileVersion { get; }
    public IReadOnlyList<NwLevelLink> Links { get; }
    public IReadOnlyList<NwLevelSign> Signs { get; }
    public IReadOnlyList<NwLevelNpc> Npcs { get; }
    public IReadOnlyList<NwLevelBaddy> Baddies { get; }
    public IReadOnlyList<NwLevelChest> Chests { get; }
    public IReadOnlyDictionary<int, ushort[]> Layers => _layers;

    public ushort GetTile(int layer, int x, int y)
    {
        return _layers.TryGetValue(layer, out var tiles)
            ? tiles[x + y * 64]
            : (ushort)0;
    }

    internal void SetTile(int layer, int x, int y, ushort tile)
    {
        if (!_layers.TryGetValue(layer, out var tiles))
        {
            tiles = new ushort[64 * 64];
            _layers[layer] = tiles;
        }

        tiles[x + y * 64] = tile;
    }
}

public sealed record NwLevelLink(
    string NewLevel,
    int X,
    int Y,
    int Width,
    int Height,
    string NewX,
    string NewY);

public sealed record NwLevelSign(int X, int Y, string Text);

public sealed record NwLevelNpc(string Image, float X, float Y, string Code);

public sealed record NwLevelBaddy(int X, int Y, int Type, IReadOnlyList<string> Verses);

public sealed record NwLevelChest(int X, int Y, LevelItemType ItemType, int SignIndex);

public static class NwLevelParser
{
    public static NwLevelParseResult Parse(string content, Func<string, bool>? linkTargetExists = null)
    {
        if (content.Length == 0)
        {
            return NwLevelParseResult.Failed;
        }

        var lines = SplitLinesKeepingEmpty(content);
        if (lines.Count == 0)
        {
            return NwLevelParseResult.Failed;
        }

        var snapshot = new NwLevelSnapshot(lines[0], [], [], [], [], []);
        var links = new List<NwLevelLink>();
        var signs = new List<NwLevelSign>();
        var npcs = new List<NwLevelNpc>();
        var baddies = new List<NwLevelBaddy>();
        var chests = new List<NwLevelChest>();

        for (var i = 0; i < lines.Count; i++)
        {
            var tokens = TokenizeBySpace(lines[i]);
            if (tokens.Count == 0)
            {
                continue;
            }

            switch (tokens[0])
            {
                case "BOARD":
                    ParseBoard(tokens, snapshot);
                    break;
                case "LINK":
                    ParseLink(tokens, links, linkTargetExists);
                    break;
                case "NPC":
                    ParseNpc(tokens, lines, ref i, npcs);
                    break;
                case "SIGN":
                    ParseSign(tokens, lines, ref i, signs);
                    break;
                case "BADDY":
                    ParseBaddy(tokens, lines, ref i, baddies);
                    break;
                case "CHEST":
                    ParseChest(tokens, chests);
                    break;
            }
        }

        var finalSnapshot = new NwLevelSnapshot(lines[0], links, signs, npcs, baddies, chests);
        foreach (var layer in snapshot.Layers)
        {
            Array.Copy(layer.Value, GetOrCreateLayer(finalSnapshot, layer.Key), layer.Value.Length);
        }

        return new NwLevelParseResult(true, finalSnapshot);
    }

    private static ushort[] GetOrCreateLayer(NwLevelSnapshot snapshot, int layer)
    {
        if (!snapshot.Layers.TryGetValue(layer, out var tiles))
        {
            snapshot.SetTile(layer, 0, 0, 0);
            tiles = snapshot.Layers[layer];
        }

        return tiles;
    }

    private static void ParseBoard(IReadOnlyList<string> tokens, NwLevelSnapshot snapshot)
    {
        if (tokens.Count != 6)
        {
            return;
        }

        var x = Atoi(tokens[1]);
        var y = Atoi(tokens[2]);
        var width = Atoi(tokens[3]);
        var layer = Atoi(tokens[4]);

        if (x < 0 || x > 64 || y < 0 || y > 64 || width <= 0 || x + width > 64)
        {
            return;
        }

        var tileData = tokens[5];
        if (tileData.Length < width * 2)
        {
            return;
        }

        var offset = 0;
        for (var tileX = x; tileX < x + width; tileX++)
        {
            var left = tileData[offset++];
            var top = tileData[offset++];
            var tile = (ushort)((GetBase64Position(left) << 6) + GetBase64Position(top));
            snapshot.SetTile(layer, tileX, y, tile);
        }
    }

    private static void ParseLink(
        IReadOnlyList<string> tokens,
        ICollection<NwLevelLink> links,
        Func<string, bool>? linkTargetExists)
    {
        if (tokens.Count < 8)
        {
            return;
        }

        var link = tokens.Skip(1).ToArray();
        var offset = 0;
        var level = link[0];
        if (link.Length > 7)
        {
            offset = link.Length - 7;
            level = string.Join(" ", link.Take(offset + 1));
        }

        if (linkTargetExists is null || !linkTargetExists(level))
        {
            return;
        }

        links.Add(new NwLevelLink(
            level,
            Atoi(link[1 + offset]),
            Atoi(link[2 + offset]),
            Atoi(link[3 + offset]),
            Atoi(link[4 + offset]),
            link[5 + offset],
            link[6 + offset]));
    }

    private static void ParseNpc(
        IReadOnlyList<string> tokens,
        IReadOnlyList<string> lines,
        ref int index,
        ICollection<NwLevelNpc> npcs)
    {
        if (tokens.Count < 4)
        {
            return;
        }

        var offset = 0;
        var image = tokens[1];
        if (tokens.Count > 4)
        {
            offset = tokens.Count - 4;
            image = string.Join(" ", tokens.Skip(1).Take(offset + 1));
        }

        var x = Atof(tokens[2 + offset]);
        var y = Atof(tokens[3 + offset]);
        var code = new List<string>();

        index++;
        while (index != lines.Count)
        {
            if (lines[index] == "NPCEND")
            {
                break;
            }

            code.Add(lines[index]);
            index++;
        }

        npcs.Add(new NwLevelNpc(image, x, y, JoinWithTrailingNewlines(code)));
    }

    private static void ParseSign(
        IReadOnlyList<string> tokens,
        IReadOnlyList<string> lines,
        ref int index,
        ICollection<NwLevelSign> signs)
    {
        if (tokens.Count != 3)
        {
            return;
        }

        var x = Atoi(tokens[1]);
        var y = Atoi(tokens[2]);
        var text = new List<string>();

        index++;
        while (index != lines.Count)
        {
            if (lines[index] == "SIGNEND")
            {
                break;
            }

            text.Add(lines[index]);
            index++;
        }

        signs.Add(new NwLevelSign(x, y, JoinWithTrailingNewlines(text)));
    }

    private static void ParseBaddy(
        IReadOnlyList<string> tokens,
        IReadOnlyList<string> lines,
        ref int index,
        ICollection<NwLevelBaddy> baddies)
    {
        if (tokens.Count != 4)
        {
            return;
        }

        var verses = new List<string>();
        index++;
        while (index != lines.Count)
        {
            if (lines[index] == "BADDYEND")
            {
                break;
            }

            verses.Add(lines[index]);
            index++;
        }

        baddies.Add(new NwLevelBaddy(Atoi(tokens[1]), Atoi(tokens[2]), Atoi(tokens[3]), verses));
    }

    private static void ParseChest(IReadOnlyList<string> tokens, ICollection<NwLevelChest> chests)
    {
        if (tokens.Count != 5)
        {
            return;
        }

        var itemType = LevelItemCatalog.GetItemId(tokens[3]);
        if (itemType == LevelItemType.Invalid)
        {
            return;
        }

        chests.Add(new NwLevelChest(Atoi(tokens[1]), Atoi(tokens[2]), itemType, Atoi(tokens[4])));
    }

    private static IReadOnlyList<string> SplitLinesKeepingEmpty(string content)
    {
        return content.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
    }

    private static IReadOnlyList<string> TokenizeBySpace(string line)
    {
        return line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    private static string JoinWithTrailingNewlines(IEnumerable<string> lines)
    {
        return string.Concat(lines.Select(line => line + "\n"));
    }

    private static int GetBase64Position(char c)
    {
        if (c >= 'a')
        {
            return 26 + (c - 'a');
        }

        if (c >= 'A')
        {
            return c - 'A';
        }

        if (c >= '0' && c <= '9')
        {
            return 52 + (c - '0');
        }

        return c switch
        {
            '+' => 62,
            '/' => 63,
            _ => 0,
        };
    }

    private static int Atoi(string value)
    {
        _ = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed);
        return parsed;
    }

    private static float Atof(string value)
    {
        _ = float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed);
        return parsed;
    }
}

using System.Globalization;

namespace Preagonal.GServer.Game;

public enum MapFileType
{
    BigMap = 0,
    GMap = 1,
}

public sealed record MapLevelPosition(int X, int Y);

public sealed record MapFileParseResult(bool Success, MapFileSnapshot Map)
{
    public static MapFileParseResult Failed { get; } = new(false, MapFileSnapshot.Empty);
}

public sealed class MapFileSnapshot
{
    public static MapFileSnapshot Empty { get; } = new(string.Empty, MapFileType.BigMap, false, 0, 0, [], false, [], string.Empty, string.Empty);

    private readonly Dictionary<string, MapLevelPosition> _levels;
    private readonly string[] _levelList;
    private readonly string[] _preloadLevelList;

    public MapFileSnapshot(
        string mapName,
        MapFileType type,
        bool isGroupMap,
        int width,
        int height,
        IReadOnlyList<string> levelList,
        bool loadFullMap,
        IReadOnlyList<string> preloadLevelList,
        string mapImage,
        string miniMapImage)
    {
        MapName = mapName;
        Type = type;
        IsGroupMap = isGroupMap;
        Width = width;
        Height = height;
        LoadFullMap = loadFullMap;
        MapImage = mapImage;
        MiniMapImage = miniMapImage;
        _levelList = levelList.ToArray();
        _preloadLevelList = preloadLevelList.ToArray();
        _levels = [];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = x + y * width;
                if (index >= _levelList.Length)
                    continue;

                var level = _levelList[index];
                if (level.Length != 0)
                    _levels[level] = new MapLevelPosition(x, y);
            }
        }
    }

    public string MapName { get; }
    public MapFileType Type { get; }
    public bool IsGroupMap { get; }
    public int Width { get; }
    public int Height { get; }
    public bool LoadFullMap { get; }
    public string MapImage { get; }
    public string MiniMapImage { get; }
    public IReadOnlyList<string> LevelList => _levelList;
    public IReadOnlyList<string> PreloadLevelList => _preloadLevelList;

    public string GetLevelAt(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
            return string.Empty;

        var index = x + y * Width;
        return index < _levelList.Length ? _levelList[index] : string.Empty;
    }

    public bool TryGetLevelPosition(string level, out MapLevelPosition position) =>
        _levels.TryGetValue(level, out position!);

    public IReadOnlyList<string> LevelsToPreload()
    {
        if (LoadFullMap)
            return _levelList.Where(level => level.Length != 0).ToArray();

        return _preloadLevelList;
    }
}

public static class MapFileParser
{
    public static MapFileParseResult ParseBigMap(string mapName, string content, bool isGroupMap = false)
    {
        var mapData = new List<IReadOnlyList<string>>();
        var width = 0;
        var height = 0;

        foreach (var rawLine in SplitLines(content))
        {
            var line = rawLine.Replace("\r", string.Empty, StringComparison.Ordinal).Trim();
            if (line.Length == 0)
                continue;

            var levelList = GUntokenize(line).Split('\n');
            var trailingEmpty = 0;
            foreach (var level in levelList)
                trailingEmpty = level.Length == 0 ? trailingEmpty + 1 : 0;

            var currentWidth = levelList.Length - trailingEmpty;
            height++;
            if (width < currentWidth)
                width = currentWidth;

            mapData.Add(levelList);
        }

        var flattened = new string[width * height];
        for (var y = 0; y < mapData.Count; y++)
        {
            for (var x = 0; x < mapData[y].Count; x++)
            {
                if (x >= width)
                    continue;

                flattened[x + y * width] = mapData[y][x].ToLowerInvariant();
            }
        }

        return new MapFileParseResult(
            true,
            new MapFileSnapshot(
                mapName,
                MapFileType.BigMap,
                isGroupMap,
                width,
                height,
                flattened.Select(level => level ?? string.Empty).ToArray(),
                loadFullMap: false,
                preloadLevelList: [],
                mapImage: string.Empty,
                miniMapImage: string.Empty));
    }

    public static MapFileParseResult ParseGMap(string mapName, string content, bool isGroupMap = false)
    {
        var lines = SplitLines(content).ToArray();
        var width = 0;
        var height = 0;
        var loadFullMap = false;
        var mapImage = string.Empty;
        var miniMapImage = string.Empty;
        var levelList = Array.Empty<string>();
        var preloadLevelList = new List<string>();

        for (var i = 0; i < lines.Length; ++i)
        {
            var line = lines[i].Replace("\r", string.Empty, StringComparison.Ordinal);
            var tokens = TokenizeBySpace(line);
            if (tokens.Count == 0)
                continue;

            switch (tokens[0])
            {
                case "WIDTH":
                    if (tokens.Count == 2)
                        width = Atoi(tokens[1]);
                    break;
                case "HEIGHT":
                    if (tokens.Count == 2)
                        height = Atoi(tokens[1]);
                    break;
                case "GENERATED":
                    break;
                case "LEVELNAMES":
                    (levelList, i) = ReadGMapLevelNames(lines, i, width, height);
                    break;
                case "MAPIMG":
                    if (tokens.Count == 2)
                        mapImage = tokens[1];
                    break;
                case "MINIMAPIMG":
                    if (tokens.Count == 2)
                        miniMapImage = tokens[1];
                    break;
                case "NOAUTOMAPPING":
                    break;
                case "LOADFULLMAP":
                    loadFullMap = true;
                    break;
                case "LOADATSTART":
                    loadFullMap = false;
                    (preloadLevelList, i) = ReadLoadAtStart(lines, i);
                    break;
            }
        }

        return new MapFileParseResult(
            true,
            new MapFileSnapshot(
                mapName,
                MapFileType.GMap,
                isGroupMap,
                width,
                height,
                levelList,
                loadFullMap,
                preloadLevelList,
                mapImage,
                miniMapImage));
    }

    private static (string[] LevelList, int Index) ReadGMapLevelNames(IReadOnlyList<string> lines, int index, int width, int height)
    {
        index++;
        var y = 0;
        var levelMap = new string[Math.Max(0, width * height)];

        while (index < lines.Count)
        {
            var line = lines[index].Replace("\r", string.Empty, StringComparison.Ordinal).Trim();
            if (line.Length == 0)
            {
                index++;
                continue;
            }

            if (line == "LEVELNAMESEND")
                break;

            if (y < height)
            {
                var x = 0;
                var names = TokenizeByNewlineWithoutEmpty(GUntokenize(line));
                foreach (var levelName in names)
                {
                    if (x < width)
                    {
                        var lower = levelName.ToLowerInvariant();
                        levelMap[x + y * width] = lower == "\r" ? string.Empty : lower;
                        x++;
                    }
                }

                y++;
            }

            index++;
        }

        return (levelMap.Select(level => level ?? string.Empty).ToArray(), index);
    }

    private static (List<string> PreloadLevelList, int Index) ReadLoadAtStart(IReadOnlyList<string> lines, int index)
    {
        var preload = new List<string>();
        index++;

        while (index < lines.Count)
        {
            var line = lines[index].Replace("\r", string.Empty, StringComparison.Ordinal);
            if (line == "LOADATSTARTEND")
                break;

            var names = TokenizeByNewlineWithoutEmpty(GUntokenize(line));
            foreach (var levelName in names)
                preload.Add(levelName.ToLowerInvariant());

            index++;
        }

        return (preload, index);
    }

    private static IEnumerable<string> SplitLines(string content) =>
        content.Split('\n');

    private static IReadOnlyList<string> TokenizeBySpace(string line) =>
        line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static IReadOnlyList<string> TokenizeByNewlineWithoutEmpty(string line) =>
        line.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static string GUntokenize(string value)
    {
        if (value.Length == 0)
            return string.Empty;

        var output = new System.Text.StringBuilder(value.Length + 5);
        var isQuoted = false;
        var i = 0;
        if (value[0] == '"')
        {
            isQuoted = true;
            i++;
        }

        for (; i < value.Length; i++)
        {
            if (value[i] == ',' && !isQuoted)
            {
                output.Append('\n');
                while (i + 1 < value.Length && value[i + 1] == ' ')
                    i++;

                if (i + 1 < value.Length && value[i + 1] == '"')
                {
                    isQuoted = true;
                    i++;
                }
            }
            else if (value[i] == '"')
            {
                if (isQuoted)
                {
                    if (i + 1 < value.Length)
                    {
                        if (value[i + 1] == '"')
                        {
                            output.Append('"');
                            i++;
                        }
                        else if (value[i + 1] == ',')
                        {
                            isQuoted = false;
                        }
                    }
                }
                else
                {
                    output.Append(value[i]);
                }
            }
            else if (value[i] == '\\')
            {
                if (i + 1 < value.Length && value[i + 1] == '\\')
                {
                    output.Append('\\');
                    i++;
                }
            }
            else
            {
                output.Append(value[i]);
            }
        }

        return output.ToString();
    }

    private static int Atoi(string value)
    {
        _ = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed);
        return parsed;
    }
}

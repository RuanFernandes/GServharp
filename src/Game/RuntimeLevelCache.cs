namespace Preagonal.GServer.Game;

public sealed record RuntimeLevelMapBinding(RuntimeMap Map, MapFileSnapshot Snapshot)
{
    public static RuntimeLevelMapBinding FromMapFile(RuntimeMap map, MapFileSnapshot snapshot) =>
        new(map, snapshot);
}

public sealed class RuntimeLevelCache
{
    private readonly List<RuntimeLevel> _levels = [];
    private readonly List<RuntimeLevelMapBinding> _maps = [];

    public RuntimeLevelCache(IEnumerable<RuntimeLevelMapBinding>? maps = null)
    {
        if (maps is not null)
            _maps.AddRange(maps);
    }

    public IReadOnlyList<RuntimeLevel> Levels => _levels;

    public RuntimeLevel CreateLevel(string levelName)
    {
        var level = new RuntimeLevel(levelName);
        _levels.Add(level);
        return level;
    }

    public RuntimeLevel? FindOrLoad(
        string levelName,
        Func<string, RuntimeLevel?> loadLevel,
        bool loadAbsolute = false,
        Func<string, bool>? isLoadAbsoluteIndexed = null,
        Action<string>? loadAbsoluteIndexMissing = null)
    {
        var lowerLevelName = levelName.ToLowerInvariant();
        foreach (var level in _levels)
        {
            if (string.Equals(level.LevelName.ToLowerInvariant(), lowerLevelName, StringComparison.Ordinal))
                return level;
        }

        if (loadAbsolute && isLoadAbsoluteIndexed?.Invoke(levelName) != true)
            loadAbsoluteIndexMissing?.Invoke(levelName);

        var loaded = loadLevel(levelName);
        if (loaded is null)
            return null;

        AttachToFirstMatchingMap(loaded, lowerLevelName);
        _levels.Add(loaded);
        return loaded;
    }

    public void ReplaceMaps(IEnumerable<RuntimeLevelMapBinding> maps)
    {
        _maps.Clear();
        _maps.AddRange(maps);

        foreach (var level in _levels)
            AttachToFirstMatchingMap(level, level.LevelName.ToLowerInvariant());
    }

    public void Clear()
    {
        foreach (var level in _levels)
            level.SetMap(null);

        _levels.Clear();
        _maps.Clear();
    }

    private void AttachToFirstMatchingMap(RuntimeLevel level, string lowerLevelName)
    {
        foreach (var binding in _maps)
        {
            if (binding.Snapshot.TryGetLevelPosition(lowerLevelName, out var position))
            {
                level.SetMap(binding.Map, position.X, position.Y);
                return;
            }
        }

        level.SetMap(null);
    }
}

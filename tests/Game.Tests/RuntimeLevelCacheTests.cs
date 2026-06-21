using Preagonal.GServer.Game;

namespace Preagonal.GServer.Game.Tests;

public sealed class RuntimeLevelCacheTests
{
    [Fact]
    public void FindOrLoadReturnsFirstCachedCaseInsensitiveMatchWithoutLoading()
    {
        var cache = new RuntimeLevelCache();
        var first = cache.CreateLevel("Start.NW");
        _ = cache.CreateLevel("start.nw");
        var loadCalls = 0;

        var found = cache.FindOrLoad("START.NW", _ =>
        {
            loadCalls++;
            return new RuntimeLevel("loaded.nw");
        });

        Assert.Same(first, found);
        Assert.Equal(0, loadCalls);
        Assert.Equal(["Start.NW", "start.nw"], cache.Levels.Select(level => level.LevelName));
    }

    [Fact]
    public void FindOrLoadDoesNotAppendWhenLoaderFails()
    {
        var cache = new RuntimeLevelCache();

        var found = cache.FindOrLoad("missing.nw", _ => null);

        Assert.Null(found);
        Assert.Empty(cache.Levels);
    }

    [Fact]
    public void FindOrLoadAppendsLoadedLevelAndAttachesFirstMatchingMapByRequestedLowerName()
    {
        var firstMap = new RuntimeMap("first.gmap", RuntimeMapType.Gmap);
        var secondMap = new RuntimeMap("second.gmap", RuntimeMapType.Gmap);
        var maps = new[]
        {
            RuntimeLevelMapBinding.FromMapFile(firstMap, MapFileParser.ParseGMap(
                "first.gmap",
                """
                WIDTH 1
                HEIGHT 1
                LEVELNAMES
                START.NW
                LEVELNAMESEND
                """).Map),
            RuntimeLevelMapBinding.FromMapFile(secondMap, MapFileParser.ParseGMap(
                "second.gmap",
                """
                WIDTH 1
                HEIGHT 1
                LEVELNAMES
                start.nw
                LEVELNAMESEND
                """).Map)
        };

        var cache = new RuntimeLevelCache(maps);
        var loaded = new RuntimeLevel("actual-different-name.nw");

        var found = cache.FindOrLoad("START.NW", _ => loaded);

        Assert.Same(loaded, found);
        Assert.Equal([loaded], cache.Levels);
        Assert.Same(firstMap, loaded.Map);
        Assert.Equal(0, loaded.MapX);
        Assert.Equal(0, loaded.MapY);
    }

    [Fact]
    public void FindOrLoadRunsLoadAbsoluteIndexMutationBeforeLoadingWhenRequested()
    {
        var cache = new RuntimeLevelCache();
        var calls = new List<string>();

        var loaded = cache.FindOrLoad(
            "levels/start.nw",
            levelName =>
            {
                calls.Add($"load:{levelName}");
            return new RuntimeLevel(levelName);
        },
        loadAbsolute: true,
        isLoadAbsoluteIndexed: _ => false,
        loadAbsoluteIndexMissing: levelName => calls.Add($"index:{levelName}"));

        Assert.NotNull(loaded);
        Assert.Equal(["index:levels/start.nw", "load:levels/start.nw"], calls);
    }

    [Fact]
    public void FindOrLoadSkipsLoadAbsoluteIndexMutationWhenRequestedNameIsAlreadyIndexed()
    {
        var cache = new RuntimeLevelCache();
        var calls = new List<string>();

        var loaded = cache.FindOrLoad(
            "levels/start.nw",
            levelName =>
            {
                calls.Add($"load:{levelName}");
                return new RuntimeLevel(levelName);
            },
            loadAbsolute: true,
            isLoadAbsoluteIndexed: _ => true,
            loadAbsoluteIndexMissing: levelName => calls.Add($"index:{levelName}"));

        Assert.NotNull(loaded);
        Assert.Equal(["load:levels/start.nw"], calls);
    }

    [Fact]
    public void ReloadMapsReattachesExistingLevelsToFirstMatchingMapAndClearsMissing()
    {
        var cache = new RuntimeLevelCache();
        var start = cache.CreateLevel("start.nw");
        var orphan = cache.CreateLevel("orphan.nw");
        var oldMap = new RuntimeMap("old.gmap", RuntimeMapType.Gmap);
        start.SetMap(oldMap, 9, 9);
        orphan.SetMap(oldMap, 8, 8);

        var newMap = new RuntimeMap("new.gmap", RuntimeMapType.Gmap);
        cache.ReplaceMaps(
        [
            RuntimeLevelMapBinding.FromMapFile(newMap, MapFileParser.ParseGMap(
                "new.gmap",
                """
                WIDTH 2
                HEIGHT 1
                LEVELNAMES
                missing.nw, START.NW
                LEVELNAMESEND
                """).Map)
        ]);

        Assert.Same(newMap, start.Map);
        Assert.Equal(1, start.MapX);
        Assert.Equal(0, start.MapY);
        Assert.Null(orphan.Map);
        Assert.Equal(0, orphan.MapX);
        Assert.Equal(0, orphan.MapY);
    }
}

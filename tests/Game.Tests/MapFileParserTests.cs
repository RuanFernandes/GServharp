using Preagonal.GServer.Game;

namespace Preagonal.GServer.Game.Tests;

public sealed class MapFileParserTests
{
    [Fact]
    public void ParseBigMapUsesGuntokenizeRowsLowercasesNamesAndIgnoresTrailingEmptyWidth()
    {
        var map = MapFileParser.ParseBigMap(
            "worldmap.txt",
            """
            start.nw, second.nw,,
            "level, with comma.nw", THIRD.NW
            """,
            isGroupMap: true);

        Assert.True(map.Success);
        Assert.Equal(MapFileType.BigMap, map.Map.Type);
        Assert.True(map.Map.IsGroupMap);
        Assert.Equal("worldmap.txt", map.Map.MapName);
        Assert.Equal(2, map.Map.Width);
        Assert.Equal(2, map.Map.Height);
        Assert.Equal("start.nw", map.Map.GetLevelAt(0, 0));
        Assert.Equal("second.nw", map.Map.GetLevelAt(1, 0));
        Assert.Equal("level, with comma.nw", map.Map.GetLevelAt(0, 1));
        Assert.Equal("third.nw", map.Map.GetLevelAt(1, 1));
        Assert.True(map.Map.TryGetLevelPosition("THIRD.NW".ToLowerInvariant(), out var position));
        Assert.Equal((1, 1), (position.X, position.Y));
        Assert.False(map.Map.TryGetLevelPosition("THIRD.NW", out _));
    }

    [Fact]
    public void ParseBigMapPreservesInteriorEmptyCellsAndReturnsEmptyForOutOfRange()
    {
        var map = MapFileParser.ParseBigMap(
            "worldmap.txt",
            """
            left.nw,,right.nw
            solo.nw
            """);

        Assert.True(map.Success);
        Assert.Equal(3, map.Map.Width);
        Assert.Equal(2, map.Map.Height);
        Assert.Equal(string.Empty, map.Map.GetLevelAt(1, 0));
        Assert.Equal("right.nw", map.Map.GetLevelAt(2, 0));
        Assert.Equal("solo.nw", map.Map.GetLevelAt(0, 1));
        Assert.Equal(string.Empty, map.Map.GetLevelAt(2, 1));
        Assert.Equal(string.Empty, map.Map.GetLevelAt(-1, 0));
        Assert.Equal(string.Empty, map.Map.GetLevelAt(0, 2));
    }

    [Fact]
    public void ParseGMapReadsDimensionsLevelNamesImagesAndLoadFullMap()
    {
        var map = MapFileParser.ParseGMap(
            "world.gmap",
            """
            WIDTH 3
            HEIGHT 2
            GENERATED ignored
            MAPIMG map.png
            MINIMAPIMG mini.png
            NOAUTOMAPPING
            LOADFULLMAP
            LEVELNAMES
            start.nw, SECOND.NW
            "third level.nw", , fourth.nw
            LEVELNAMESEND
            """);

        Assert.True(map.Success);
        Assert.Equal(MapFileType.GMap, map.Map.Type);
        Assert.False(map.Map.IsGroupMap);
        Assert.Equal(3, map.Map.Width);
        Assert.Equal(2, map.Map.Height);
        Assert.Equal("map.png", map.Map.MapImage);
        Assert.Equal("mini.png", map.Map.MiniMapImage);
        Assert.True(map.Map.LoadFullMap);
        Assert.Equal(["start.nw", "second.nw", "third level.nw", "fourth.nw"], map.Map.LevelsToPreload());
        Assert.Equal("start.nw", map.Map.GetLevelAt(0, 0));
        Assert.Equal("second.nw", map.Map.GetLevelAt(1, 0));
        Assert.Equal(string.Empty, map.Map.GetLevelAt(2, 0));
        Assert.Equal("third level.nw", map.Map.GetLevelAt(0, 1));
        Assert.Equal("fourth.nw", map.Map.GetLevelAt(1, 1));
        Assert.Equal(string.Empty, map.Map.GetLevelAt(2, 1));
    }

    [Fact]
    public void ParseGMapLoadAtStartDisablesFullMapAndLowercasesPreloadNames()
    {
        var map = MapFileParser.ParseGMap(
            "group.gmap",
            """
            WIDTH 1
            HEIGHT 1
            LOADFULLMAP
            LEVELNAMES
            first.nw
            LEVELNAMESEND
            LOADATSTART
            START.NW, "Second Level.NW"
            LOADATSTARTEND
            """,
            isGroupMap: true);

        Assert.True(map.Success);
        Assert.True(map.Map.IsGroupMap);
        Assert.False(map.Map.LoadFullMap);
        Assert.Equal(["start.nw", "second level.nw"], map.Map.LevelsToPreload());
    }

    [Fact]
    public void ParseGMapIgnoresMalformedDirectivesAndRowsPastHeight()
    {
        var map = MapFileParser.ParseGMap(
            "world.gmap",
            """
            WIDTH
            HEIGHT 1
            WIDTH 2
            MAPIMG too many tokens ignored
            LEVELNAMES
            first.nw, second.nw
            third.nw, fourth.nw
            LEVELNAMESEND
            """);

        Assert.True(map.Success);
        Assert.Equal(2, map.Map.Width);
        Assert.Equal(1, map.Map.Height);
        Assert.Equal(string.Empty, map.Map.MapImage);
        Assert.Equal("first.nw", map.Map.GetLevelAt(0, 0));
        Assert.Equal("second.nw", map.Map.GetLevelAt(1, 0));
        Assert.False(map.Map.TryGetLevelPosition("third.nw", out _));
    }
}

using GServ.Game;

namespace GServ.Game.Tests;

public sealed class NwLevelParserTests
{
    [Fact]
    public void ParseRejectsEmptyContent()
    {
        var result = NwLevelParser.Parse("");

        Assert.False(result.Success);
    }

    [Fact]
    public void ParseStoresFirstLineAsFileVersionAndIgnoresUnknownLines()
    {
        var result = NwLevelParser.Parse("""
            GLEVNW01
            UNKNOWN whatever
            BOARD 0 0 1 0 AB
            """);

        Assert.True(result.Success);
        Assert.Equal("GLEVNW01", result.Level.FileVersion);
        Assert.Equal(1, result.Level.GetTile(0, 0, 0));
        Assert.Empty(result.Level.Signs);
        Assert.Empty(result.Level.Npcs);
        Assert.Empty(result.Level.Baddies);
    }

    [Fact]
    public void ParseBoardWritesTilesUsingCppBase64Pairs()
    {
        var result = NwLevelParser.Parse("""
            GLEVNW01
            BOARD 1 2 3 0 AB+/@?
            """);

        Assert.True(result.Success);
        Assert.Equal(1, result.Level.GetTile(0, 1, 2));
        Assert.Equal(4031, result.Level.GetTile(0, 2, 2));
        Assert.Equal(0, result.Level.GetTile(0, 3, 2));
    }

    [Fact]
    public void ParseBoardIgnoresInvalidGeometryOrShortTileData()
    {
        var result = NwLevelParser.Parse("""
            GLEVNW01
            BOARD -1 0 1 0 AB
            BOARD 63 0 2 0 ABCD
            BOARD 0 1 2 0 AB
            """);

        Assert.True(result.Success);
        Assert.Equal(0, result.Level.GetTile(0, 0, 0));
        Assert.Equal(0, result.Level.GetTile(0, 63, 0));
        Assert.Equal(0, result.Level.GetTile(0, 0, 1));
    }

    [Fact]
    public void ParseSignPreservesTextLinesWithCppTrailingNewline()
    {
        var result = NwLevelParser.Parse("""
            GLEVNW01
            SIGN 4 5
            first line
            second line
            SIGNEND
            """);

        Assert.True(result.Success);
        var sign = Assert.Single(result.Level.Signs);
        Assert.Equal(4, sign.X);
        Assert.Equal(5, sign.Y);
        Assert.Equal("first line\nsecond line\n", sign.Text);
    }

    [Fact]
    public void ParseNpcAllowsImageNamesWithSpacesAndPreservesCodeWithTrailingNewlines()
    {
        var result = NwLevelParser.Parse("""
            GLEVNW01
            NPC image with spaces.png 12.5 13.25
            if (created) {
            }
            NPCEND
            """);

        Assert.True(result.Success);
        var npc = Assert.Single(result.Level.Npcs);
        Assert.Equal("image with spaces.png", npc.Image);
        Assert.Equal(12.5f, npc.X);
        Assert.Equal(13.25f, npc.Y);
        Assert.Equal("if (created) {\n}\n", npc.Code);
    }

    [Fact]
    public void ParseBaddyPreservesVerseLinesUntilTerminator()
    {
        var result = NwLevelParser.Parse("""
            GLEVNW01
            BADDY 10 11 2
            see
            hurt
            attack
            BADDYEND
            """);

        Assert.True(result.Success);
        var baddy = Assert.Single(result.Level.Baddies);
        Assert.Equal(10, baddy.X);
        Assert.Equal(11, baddy.Y);
        Assert.Equal(2, baddy.Type);
        Assert.Equal(["see", "hurt", "attack"], baddy.Verses);
    }

    [Fact]
    public void ParseLinkOnlyAddsWhenTargetResolverFindsTheResolvedLevelName()
    {
        var result = NwLevelParser.Parse(
            """
            GLEVNW01
            LINK target level.nw 1 2 3 4 5.5 6.5
            LINK missing.nw 7 8 9 10 11 12
            """,
            linkTargetExists: levelName => levelName == "target level.nw");

        Assert.True(result.Success);
        var link = Assert.Single(result.Level.Links);
        Assert.Equal("target level.nw", link.NewLevel);
        Assert.Equal(1, link.X);
        Assert.Equal(2, link.Y);
        Assert.Equal(3, link.Width);
        Assert.Equal(4, link.Height);
        Assert.Equal("5.5", link.NewX);
        Assert.Equal("6.5", link.NewY);
    }

    [Fact]
    public void ParseChestOnlyAddsKnownLevelItemNames()
    {
        var result = NwLevelParser.Parse("""
            GLEVNW01
            CHEST 10 11 redrupee 3
            CHEST 12 13 missing 4
            """);

        Assert.True(result.Success);
        var chest = Assert.Single(result.Level.Chests);
        Assert.Equal(10, chest.X);
        Assert.Equal(11, chest.Y);
        Assert.Equal(LevelItemType.RedRupee, chest.ItemType);
        Assert.Equal(3, chest.SignIndex);
    }
}

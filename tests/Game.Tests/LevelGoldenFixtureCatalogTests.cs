using System.Text;
using GServ.Game;

namespace GServ.Game.Tests;

public sealed class LevelGoldenFixtureCatalogTests
{
    [Theory]
    [MemberData(nameof(ExtensionFixtures))]
    public void ExtensionFixturesLockCppExtensionFirstSelection(string levelName, LevelFileFormat expected)
    {
        Assert.Equal(expected, LevelFileFormatDetector.FromExtension(levelName));
    }

    [Theory]
    [MemberData(nameof(HeaderFixtures))]
    public void HeaderFixturesLockCppEightByteSignatureSelection(byte[] header, LevelFileFormat expected)
    {
        Assert.Equal(expected, LevelFileFormatDetector.DetectFromHeader(header));
    }

    [Fact]
    public void RepresentativeNwBoardPacketFixtureMatchesParserAndPacketBuilder()
    {
        var parsed = NwLevelParser.Parse(LevelGoldenFixtures.RepresentativeNwBoardSource);

        var packet = NwLevelPacketBuilder.BuildBoardPacket(parsed.Level);

        Assert.Equal(LevelGoldenFixtures.RepresentativeNwBoardPacketPrefix, packet[..5]);
        Assert.Equal(1 + 64 * 64 * 2 + 1, packet.Length);
        Assert.Equal(10, packet[^1]);
    }

    [Fact]
    public void RepresentativeNwLayerPacketFixtureMatchesParserAndPacketBuilder()
    {
        var parsed = NwLevelParser.Parse(LevelGoldenFixtures.RepresentativeNwLayerSource);

        var packet = NwLevelPacketBuilder.BuildLayerPacket(parsed.Level, 1);

        Assert.Equal(LevelGoldenFixtures.RepresentativeNwLayerPacketPrefix, packet[..8]);
        Assert.Equal(1 + 5 + 64 * 64 * 2 + 1, packet.Length);
        Assert.Equal(10, packet[^1]);
    }

    [Fact]
    public void RepresentativeNwLinkPacketFixtureMatchesParserAndPacketBuilder()
    {
        var parsed = NwLevelParser.Parse(
            LevelGoldenFixtures.RepresentativeNwLinkSource,
            linkTargetExists: levelName => levelName == "target level.nw");

        Assert.Equal(LevelGoldenFixtures.RepresentativeNwLinkPacket, NwLevelPacketBuilder.BuildLinksPacket(parsed.Level));
    }

    [Fact]
    public void RepresentativeNwSignPacketFixtureMatchesParserAndPacketBuilder()
    {
        var parsed = NwLevelParser.Parse(LevelGoldenFixtures.RepresentativeNwSignSource);

        Assert.Equal(LevelGoldenFixtures.RepresentativeNwSignPacket, NwLevelPacketBuilder.BuildSignsPacket(parsed.Level));
    }

    [Fact]
    public void RepresentativeNwChestPacketFixtureMatchesParserAndPacketBuilder()
    {
        var parsed = NwLevelParser.Parse(LevelGoldenFixtures.RepresentativeNwChestSource);

        var packet = NwLevelPacketBuilder.BuildChestPacket(
            parsed.Level,
            "start.nw",
            chestKey => chestKey == "12:13:start.nw");

        Assert.Equal(LevelGoldenFixtures.RepresentativeNwChestPacket, packet);
    }

    public static TheoryData<string, LevelFileFormat> ExtensionFixtures()
    {
        var data = new TheoryData<string, LevelFileFormat>();
        foreach (var fixture in LevelGoldenFixtures.ExtensionFormatFixtures)
        {
            data.Add(fixture.LevelName, fixture.Format);
        }

        return data;
    }

    public static TheoryData<byte[], LevelFileFormat> HeaderFixtures()
    {
        var data = new TheoryData<byte[], LevelFileFormat>();
        foreach (var fixture in LevelGoldenFixtures.HeaderFormatFixtures)
        {
            data.Add(Encoding.ASCII.GetBytes(fixture.Header), fixture.Format);
        }

        return data;
    }
}

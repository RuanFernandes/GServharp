using System.Text;
using Preagonal.GServer.Game;

namespace Preagonal.GServer.Game.Tests;

public sealed class LevelFileFormatDetectionTests
{
    [Theory]
    [InlineData("start.nw", LevelFileFormat.Nw)]
    [InlineData("start.graal", LevelFileFormat.Graal)]
    [InlineData("start.zelda", LevelFileFormat.Zelda)]
    [InlineData("start.txt", LevelFileFormat.Unknown)]
    [InlineData("START.NW", LevelFileFormat.Unknown)]
    public void FromExtensionUsesTheExactCppExtensionChecks(string levelName, LevelFileFormat expected)
    {
        Assert.Equal(expected, LevelFileFormatDetector.FromExtension(levelName));
    }

    [Theory]
    [InlineData("GLEVNW01", LevelFileFormat.Nw)]
    [InlineData("GR-V1.03", LevelFileFormat.Graal)]
    [InlineData("GR-V1.02", LevelFileFormat.Graal)]
    [InlineData("GR-V1.01", LevelFileFormat.Graal)]
    [InlineData("Z3-V1.04", LevelFileFormat.Zelda)]
    [InlineData("Z3-V1.03", LevelFileFormat.Zelda)]
    [InlineData("UNKNOWN!", LevelFileFormat.Unknown)]
    public void DetectFromHeaderUsesTheCppEightByteMagicValues(string header, LevelFileFormat expected)
    {
        Assert.Equal(expected, LevelFileFormatDetector.DetectFromHeader(Encoding.ASCII.GetBytes(header)));
    }

    [Fact]
    public void ChooseUsesExtensionBeforeHeaderDetection()
    {
        var nwHeader = Encoding.ASCII.GetBytes("GLEVNW01");

        Assert.Equal(LevelFileFormat.Graal, LevelFileFormatDetector.Choose("level.graal", nwHeader));
    }

    [Fact]
    public void DetectFromHeaderRequiresEightBytes()
    {
        Assert.Equal(LevelFileFormat.Unknown, LevelFileFormatDetector.DetectFromHeader("GLEVNW0"u8));
    }
}

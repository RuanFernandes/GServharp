using Preagonal.GServer.Game;
using System.Text;

namespace Preagonal.GServer.Game.Tests;

public sealed class ZeldaLevelParserTests
{
    [Theory]
    [InlineData("Z3-V1.03")]
    [InlineData("Z3-V1.04")]
    public void ParseAcceptsConfirmedVersionsAndDecodesTwelveBitTileRle(string version)
    {
        var content = BuildLevel(
            version,
            [
                2,
                ControlCode(4),
                6,
                ControlCode(0x100 | 2),
                8,
                9,
            ]);

        var result = ZeldaLevelParser.Parse(content);

        Assert.True(result.Success);
        Assert.Equal(version, result.Level.FileVersion);
        Assert.Equal(2, result.Level.GetTile(0, 0, 0));
        Assert.Equal(6, result.Level.GetTile(0, 1, 0));
        Assert.Equal(6, result.Level.GetTile(0, 2, 0));
        Assert.Equal(6, result.Level.GetTile(0, 3, 0));
        Assert.Equal(6, result.Level.GetTile(0, 4, 0));
        Assert.Equal(8, result.Level.GetTile(0, 5, 0));
        Assert.Equal(9, result.Level.GetTile(0, 6, 0));
        Assert.Equal(8, result.Level.GetTile(0, 7, 0));
        Assert.Equal(9, result.Level.GetTile(0, 8, 0));
    }

    [Fact]
    public void ParseRejectsUnknownVersion()
    {
        var result = ZeldaLevelParser.Parse(Encoding.Latin1.GetBytes("Z3-V9.99"));

        Assert.False(result.Success);
    }

    [Fact]
    public void ParseDelegatesGraalHeaderToGraalParser()
    {
        var content = BuildGraalLevel(
            "GR-V1.03",
            tileCodes: [77],
            chests: $"{G((int)LevelItemType.RedRupee)}{G(2)}{G((int)LevelItemType.BlueRupee)}{G(3)}\n#\n");

        var result = ZeldaLevelParser.Parse(content);

        Assert.True(result.Success);
        Assert.Equal("GR-V1.03", result.Level.FileVersion);
        Assert.Equal(77, result.Level.GetTile(0, 0, 0));
        var chest = Assert.Single(result.Level.Chests);
        Assert.Equal(LevelItemType.BlueRupee, chest.ItemType);
    }

    [Fact]
    public void ParseReadsLinksBaddiesAndSignsInCppOrder()
    {
        var content = BuildLevel(
            "Z3-V1.04",
            tileCodes: [42],
            links: "target level.zelda 1 2 3 4 5 6\nmissing.zelda 7 8 9 10 11 12\n#\n",
            baddies: "\x2a\x2b\x2csee\\hurt\n\xff\xff\xff\n",
            signs: $"{G(14)}{G(15)}Hello zelda sign\n\n");

        var result = ZeldaLevelParser.Parse(content, linkTargetExists: level => level == "target level.zelda");

        Assert.True(result.Success);
        Assert.Empty(result.Level.Npcs);
        Assert.Empty(result.Level.Chests);

        var link = Assert.Single(result.Level.Links);
        Assert.Equal("target level.zelda", link.NewLevel);
        Assert.Equal(1, link.X);
        Assert.Equal(2, link.Y);
        Assert.Equal(3, link.Width);
        Assert.Equal(4, link.Height);
        Assert.Equal("5", link.NewX);
        Assert.Equal("6", link.NewY);

        var baddy = Assert.Single(result.Level.Baddies);
        Assert.Equal(42, baddy.X);
        Assert.Equal(43, baddy.Y);
        Assert.Equal(44, baddy.Type);
        Assert.Equal(["see", "hurt"], baddy.Verses);

        var sign = Assert.Single(result.Level.Signs);
        Assert.Equal(14, sign.X);
        Assert.Equal(15, sign.Y);
        Assert.Equal("Hello zelda sign", sign.Text);
    }

    [Fact]
    public void ParseVersion103BaddyDoesNotConsumeVerseLine()
    {
        var content = BuildLevel(
            "Z3-V1.03",
            tileCodes: [1],
            links: "#\n",
            baddies: "\x2a\x2b\x2c\xff\xff\xff\n",
            signs: $"{G(14)}{G(15)}Actual sign\n\n");

        var result = ZeldaLevelParser.Parse(content);

        Assert.True(result.Success);
        var baddy = Assert.Single(result.Level.Baddies);
        Assert.Equal(42, baddy.X);
        Assert.Equal(43, baddy.Y);
        Assert.Equal(44, baddy.Type);
        Assert.Empty(baddy.Verses);
        var sign = Assert.Single(result.Level.Signs);
        Assert.Equal("Actual sign", sign.Text);
    }

    private static byte[] BuildLevel(
        string version,
        IReadOnlyList<int> tileCodes,
        string links = "\n",
        string baddies = "\xff\xff\xff\n",
        string signs = "\n")
    {
        var bytes = new List<byte>();
        bytes.AddRange(Encoding.Latin1.GetBytes(version));
        bytes.AddRange(PackTiles(tileCodes));
        bytes.AddRange(Encoding.Latin1.GetBytes(links));
        bytes.AddRange(Encoding.Latin1.GetBytes(baddies));
        bytes.AddRange(Encoding.Latin1.GetBytes(signs));
        return bytes.ToArray();
    }

    private static byte[] BuildGraalLevel(
        string version,
        IReadOnlyList<int> tileCodes,
        string links = "\n",
        string baddies = "\xff\xff\xff\n",
        string npcs = "\n",
        string chests = "\n",
        string signs = "\n")
    {
        var bytes = new List<byte>();
        bytes.AddRange(Encoding.Latin1.GetBytes(version));
        bytes.AddRange(PackTiles(tileCodes, bits: 13));
        bytes.AddRange(Encoding.Latin1.GetBytes(links));
        bytes.AddRange(Encoding.Latin1.GetBytes(baddies));
        bytes.AddRange(Encoding.Latin1.GetBytes(npcs));
        bytes.AddRange(Encoding.Latin1.GetBytes(chests));
        bytes.AddRange(Encoding.Latin1.GetBytes(signs));
        return bytes.ToArray();
    }

    private static byte[] PackTiles(IReadOnlyList<int> prefixCodes, int bits = 12)
    {
        var codes = new List<int>(prefixCodes);
        var simulatedBoardIndex = 0;
        var count = 1;
        var doubleMode = false;
        var pendingDoubleFirstTile = false;

        foreach (var code in prefixCodes)
        {
            if ((code & (bits == 12 ? 0x800 : 0x1000)) != 0)
            {
                doubleMode = (code & 0x100) != 0;
                count = code & 0xff;
                pendingDoubleFirstTile = false;
                continue;
            }

            if (count == 1)
            {
                simulatedBoardIndex++;
            }
            else if (doubleMode && !pendingDoubleFirstTile)
            {
                pendingDoubleFirstTile = true;
            }
            else if (doubleMode)
            {
                for (var i = 0; i < count && simulatedBoardIndex < (64 * 64) - 1; i++)
                    simulatedBoardIndex += 2;
                count = 1;
                doubleMode = false;
                pendingDoubleFirstTile = false;
            }
            else
            {
                simulatedBoardIndex += count;
                count = 1;
            }
        }

        while (simulatedBoardIndex < 64 * 64)
        {
            codes.Add(0);
            simulatedBoardIndex++;
        }

        var packed = new List<byte>();
        var buffer = 0u;
        var bitCount = 0;
        var mask = bits == 12 ? 0xfff : 0x1fff;
        foreach (var code in codes)
        {
            buffer |= (uint)(code & mask) << bitCount;
            bitCount += bits;
            while (bitCount >= 8)
            {
                packed.Add((byte)(buffer & 0xff));
                buffer >>= 8;
                bitCount -= 8;
            }
        }

        if (bitCount > 0)
            packed.Add((byte)(buffer & 0xff));

        return packed.ToArray();
    }

    private static int ControlCode(int value) => 0x800 | value;

    private static char G(int value) => (char)(value + 32);
}

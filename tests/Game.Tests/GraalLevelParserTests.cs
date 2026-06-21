using Preagonal.GServer.Game;
using System.Text;

namespace Preagonal.GServer.Game.Tests;

public sealed class GraalLevelParserTests
{
    [Theory]
    [InlineData("GR-V1.00", 12)]
    [InlineData("GR-V1.01", 13)]
    [InlineData("GR-V1.02", 13)]
    [InlineData("GR-V1.03", 13)]
    public void ParseAcceptsConfirmedVersionsAndDecodesTileRle(string version, int bits)
    {
        var content = BuildLevel(
            version,
            bits,
            [
                1,
                ControlCode(bits, 3),
                7,
                ControlCode(bits, 0x100 | 2),
                8,
                9,
            ]);

        var result = GraalLevelParser.Parse(content);

        Assert.True(result.Success);
        Assert.Equal(version, result.Level.FileVersion);
        Assert.Equal(1, result.Level.GetTile(0, 0, 0));
        Assert.Equal(7, result.Level.GetTile(0, 1, 0));
        Assert.Equal(7, result.Level.GetTile(0, 2, 0));
        Assert.Equal(7, result.Level.GetTile(0, 3, 0));
        Assert.Equal(8, result.Level.GetTile(0, 4, 0));
        Assert.Equal(9, result.Level.GetTile(0, 5, 0));
        Assert.Equal(8, result.Level.GetTile(0, 6, 0));
        Assert.Equal(9, result.Level.GetTile(0, 7, 0));
    }

    [Fact]
    public void ParseRejectsUnknownVersion()
    {
        var content = Encoding.Latin1.GetBytes("GR-V9.99");

        var result = GraalLevelParser.Parse(content);

        Assert.False(result.Success);
    }

    [Fact]
    public void ParseReadsLegacySectionsInCppOrder()
    {
        var content = BuildLevel(
            "GR-V1.03",
            bits: 13,
            tileCodes: [42],
            links: "target level.graal 1 2 3 4 5.5 6.5\nmissing.graal 7 8 9 10 11 12\n#\n",
            baddies: "\x2a\x2b\x2csee\\hurt\n\xff\xff\xff\n",
            npcs: $"{G(10)}{G(11)}image.png#if (created) {{§}}\n#\n",
            chests: $"{G(12)}{G(13)}{G((int)LevelItemType.RedRupee)}{G(4)}\n#\n",
            signs: $"{G(14)}{G(15)}Hello sign\n\n");

        var result = GraalLevelParser.Parse(content, linkTargetExists: level => level == "target level.graal");

        Assert.True(result.Success);
        var link = Assert.Single(result.Level.Links);
        Assert.Equal("target level.graal", link.NewLevel);
        Assert.Equal(1, link.X);
        Assert.Equal(2, link.Y);
        Assert.Equal(3, link.Width);
        Assert.Equal(4, link.Height);
        Assert.Equal("5.5", link.NewX);
        Assert.Equal("6.5", link.NewY);

        var baddy = Assert.Single(result.Level.Baddies);
        Assert.Equal(42, baddy.X);
        Assert.Equal(43, baddy.Y);
        Assert.Equal(44, baddy.Type);
        Assert.Equal(["see", "hurt"], baddy.Verses);

        var npc = Assert.Single(result.Level.Npcs);
        Assert.Equal("image.png", npc.Image);
        Assert.Equal(10, npc.X);
        Assert.Equal(11, npc.Y);
        Assert.Equal("if (created) {\n}", npc.Code);

        var chest = Assert.Single(result.Level.Chests);
        Assert.Equal(12, chest.X);
        Assert.Equal(13, chest.Y);
        Assert.Equal(LevelItemType.RedRupee, chest.ItemType);
        Assert.Equal(4, chest.SignIndex);

        var sign = Assert.Single(result.Level.Signs);
        Assert.Equal(14, sign.X);
        Assert.Equal(15, sign.Y);
        Assert.Equal("Hello sign", sign.Text);
    }

    [Fact]
    public void ParseVersion100DoesNotReadChestSection()
    {
        var content = BuildLevel(
            "GR-V1.00",
            bits: 12,
            tileCodes: [1],
            links: "#\n",
            baddies: "\xff\xff\xff\n",
            npcs: "#\n",
            chests: $"{G(12)}{G(13)}{G((int)LevelItemType.RedRupee)}{G(4)}\n",
            signs: $"{G(14)}{G(15)}Still parsed as sign section\n\n");

        var result = GraalLevelParser.Parse(content);

        Assert.True(result.Success);
        Assert.Empty(result.Level.Chests);
        Assert.Collection(
            result.Level.Signs,
            first =>
            {
                Assert.Equal(12, first.X);
                Assert.Equal(13, first.Y);
                Assert.Equal($"{G((int)LevelItemType.RedRupee)}{G(4)}", first.Text);
            },
            second =>
            {
                Assert.Equal(14, second.X);
                Assert.Equal(15, second.Y);
                Assert.Equal("Still parsed as sign section", second.Text);
            });
    }

    private static byte[] BuildLevel(
        string version,
        int bits,
        IReadOnlyList<int> tileCodes,
        string links = "\n",
        string baddies = "\xff\xff\xff\n",
        string npcs = "\n",
        string chests = "\n",
        string signs = "\n")
    {
        var bytes = new List<byte>();
        bytes.AddRange(Encoding.Latin1.GetBytes(version));
        bytes.AddRange(PackTiles(bits, tileCodes));
        bytes.AddRange(Encoding.Latin1.GetBytes(links));
        bytes.AddRange(Encoding.Latin1.GetBytes(baddies));
        bytes.AddRange(Encoding.Latin1.GetBytes(npcs));
        bytes.AddRange(Encoding.Latin1.GetBytes(chests));
        bytes.AddRange(Encoding.Latin1.GetBytes(signs));
        return bytes.ToArray();
    }

    private static byte[] PackTiles(int bits, IReadOnlyList<int> prefixCodes)
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

    private static int ControlCode(int bits, int value) => (bits == 12 ? 0x800 : 0x1000) | value;

    private static char G(int value) => (char)(value + 32);
}

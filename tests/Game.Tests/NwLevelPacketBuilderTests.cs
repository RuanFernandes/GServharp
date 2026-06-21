using Preagonal.GServer.Game;

namespace Preagonal.GServer.Game.Tests;

public sealed class NwLevelPacketBuilderTests
{
    [Fact]
    public void BuildBoardPacketWritesPloBoardPacketRawLittleEndianTilesAndNewline()
    {
        var parsed = NwLevelParser.Parse("""
            GLEVNW01
            BOARD 0 0 2 0 AB+/
            """);

        var packet = NwLevelPacketBuilder.BuildBoardPacket(parsed.Level);

        Assert.Equal(1 + 64 * 64 * 2 + 1, packet.Length);
        Assert.Equal(133, packet[0]);
        Assert.Equal([1, 0], packet[1..3]);
        Assert.Equal([191, 15], packet[3..5]);
        Assert.Equal(10, packet[^1]);
    }

    [Fact]
    public void BuildLayerPacketWritesLayerHeaderRawLittleEndianTilesAndNewline()
    {
        var parsed = NwLevelParser.Parse("""
            GLEVNW01
            BOARD 0 0 1 1 +/
            """);

        var packet = NwLevelPacketBuilder.BuildLayerPacket(parsed.Level, 1);

        Assert.Equal(1 + 5 + 64 * 64 * 2 + 1, packet.Length);
        Assert.Equal(139, packet[0]);
        Assert.Equal([1, 0, 0, 64, 64], packet[1..6]);
        Assert.Equal([191, 15], packet[6..8]);
        Assert.Equal(10, packet[^1]);
    }

    [Fact]
    public void BuildLinksPacketWritesPloLevelLinkAsciiLinkStringAndNewlineInOrder()
    {
        var parsed = NwLevelParser.Parse(
            """
            GLEVNW01
            LINK target level.nw 1 2 3 4 5.5 6.5
            """,
            linkTargetExists: levelName => levelName == "target level.nw");

        var packet = NwLevelPacketBuilder.BuildLinksPacket(parsed.Level);

        Assert.Equal(
            new byte[]
            {
                33,
                (byte)'t', (byte)'a', (byte)'r', (byte)'g', (byte)'e', (byte)'t', (byte)' ',
                (byte)'l', (byte)'e', (byte)'v', (byte)'e', (byte)'l', (byte)'.', (byte)'n', (byte)'w',
                (byte)' ', (byte)'1', (byte)' ', (byte)'2', (byte)' ', (byte)'3', (byte)' ', (byte)'4',
                (byte)' ', (byte)'5', (byte)'.', (byte)'5', (byte)' ', (byte)'6', (byte)'.', (byte)'5',
                10
            },
            packet);
    }

    [Fact]
    public void BuildSignsPacketWritesPloLevelSignPositionEncodedTextAndNewline()
    {
        var parsed = NwLevelParser.Parse("""
            GLEVNW01
            SIGN 4 5
            A
            SIGNEND
            """);

        var packet = NwLevelPacketBuilder.BuildSignsPacket(parsed.Level);

        Assert.Equal(new byte[] { 37, 36, 37, 32, 128, 10 }, packet);
    }

    [Fact]
    public void BuildSignsPacketEncodesUnknownCharactersAsCppHashKDecimalCode()
    {
        var parsed = NwLevelParser.Parse("""
            GLEVNW01
            SIGN 4 5
            @
            SIGNEND
            """);

        var packet = NwLevelPacketBuilder.BuildSignsPacket(parsed.Level);

        Assert.Equal(new byte[] { 37, 36, 37, 118, 42, 101, 90, 88, 102, 128, 10 }, packet);
    }

    [Fact]
    public void BuildLinksAndSignsPacketsAreEmptyForEmptyLists()
    {
        var parsed = NwLevelParser.Parse("GLEVNW01");

        Assert.Empty(NwLevelPacketBuilder.BuildLinksPacket(parsed.Level));
        Assert.Empty(NwLevelPacketBuilder.BuildSignsPacket(parsed.Level));
    }

    [Fact]
    public void BuildChestPacketUsesPlayerOwnedChestPredicate()
    {
        var parsed = NwLevelParser.Parse("""
            GLEVNW01
            CHEST 10 11 redrupee 3
            CHEST 12 13 bluerupee 4
            """);

        var packet = NwLevelPacketBuilder.BuildChestPacket(
            parsed.Level,
            "start.nw",
            chestKey => chestKey == "12:13:start.nw");

        Assert.Equal(
            new byte[]
            {
                36, 32, 42, 43, 34, 35, 10,
                36, 33, 44, 45, 10
            },
            packet);
    }
}

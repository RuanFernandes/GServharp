using Preagonal.GServer.Protocol;
using Xunit;

namespace Preagonal.GServer.Protocol.Tests;

public sealed class LevelWarpPacketParserTests
{
    [Fact]
    public void ParseLevelWarpReadsHalfTileCoordinatesAndRemainingLevelName()
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)PlayerToServerPacketId.LevelWarp);
        writer.WriteGChar(61);
        writer.WriteGChar(62);
        writer.WriteBytes("start.nw"u8);

        var packet = LevelWarpPacketParser.Parse(writer.ToArray());

        Assert.Equal(PlayerToServerPacketId.LevelWarp, packet.PacketId);
        Assert.Equal(30.5f, packet.X);
        Assert.Equal(31.0f, packet.Y);
        Assert.Equal(0, packet.ModTime);
        Assert.Equal("start.nw", packet.LevelName);
    }

    [Fact]
    public void ParseLevelWarpModReadsModifiedTimeBeforeCoordinates()
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)PlayerToServerPacketId.LevelWarpMod);
        writer.WriteGInt5(123);
        writer.WriteGChar(80);
        writer.WriteGChar(81);
        writer.WriteBytes("world_a01.nw"u8);

        var packet = LevelWarpPacketParser.Parse(writer.ToArray());

        Assert.Equal(PlayerToServerPacketId.LevelWarpMod, packet.PacketId);
        Assert.Equal(40.0f, packet.X);
        Assert.Equal(40.5f, packet.Y);
        Assert.Equal(123, packet.ModTime);
        Assert.Equal("world_a01.nw", packet.LevelName);
    }

    [Fact]
    public void ParseRejectsPacketIdThatDoesNotUseLevelWarpHandler()
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)PlayerToServerPacketId.PlayerProps);
        writer.WriteGChar(61);
        writer.WriteGChar(62);
        writer.WriteBytes("start.nw"u8);

        Assert.Throws<InvalidDataException>(() => LevelWarpPacketParser.Parse(writer.ToArray()));
    }
}

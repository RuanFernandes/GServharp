using System.Text;
using Preagonal.GServer.Protocol;
using Xunit;

namespace Preagonal.GServer.Protocol.Tests;

public sealed class WarpPacketBuilderTests
{
    [Fact]
    public void BuildWarpFailedMatchesCppPacketBody()
    {
        Assert.Equal(
            new byte[] { 47, (byte)'m', (byte)'i', (byte)'s', (byte)'s', (byte)'i', (byte)'n', (byte)'g', (byte)'.', (byte)'n', (byte)'w' },
            WarpPackets.BuildWarpFailed("missing.nw"));
    }

    [Fact]
    public void BuildPlayerWarpMatchesCppPackedPositionAndRawLevelName()
    {
        Assert.Equal(
            new byte[] { 46, 93, 94, (byte)'s', (byte)'t', (byte)'a', (byte)'r', (byte)'t', (byte)'.', (byte)'n', (byte)'w' },
            WarpPackets.BuildPlayerWarp(30.5f, 31.25f, "start.nw"));
    }

    [Fact]
    public void BuildPlayerWarp2MatchesCppPackedPositionMapAndRawMapName()
    {
        Assert.Equal(
            new byte[] { 81, 93, 94, 85, 36, 37, (byte)'w', (byte)'o', (byte)'r', (byte)'l', (byte)'d', (byte)'.', (byte)'g', (byte)'m', (byte)'a', (byte)'p' },
            WarpPackets.BuildPlayerWarp2(30.5f, 31.25f, 1.5f, 4, 5, "world.gmap"));
    }

    [Fact]
    public void BuildLevelNameMatchesFirstSendLevelPacket()
    {
        Assert.Equal(
            Encoding.ASCII.GetBytes("&start.nw").ToArray(),
            WarpPackets.BuildLevelName("start.nw"));
    }
}

using Preagonal.GServer.Network;
using Xunit;

namespace Preagonal.GServer.Network.Tests;

public sealed class SocketReceiveBufferTests
{
    [Fact]
    public void DrainFramesWaitsForTwoByteHeader()
    {
        var buffer = new SocketReceiveBuffer();

        buffer.Append([0]);

        Assert.Empty(buffer.DrainFrames());
    }

    [Fact]
    public void DrainFramesKeepsPartialPayloadUntilComplete()
    {
        var buffer = new SocketReceiveBuffer();

        buffer.Append([0, 3, 65]);
        Assert.Empty(buffer.DrainFrames());

        buffer.Append([66, 67]);

        var frame = Assert.Single(buffer.DrainFrames());
        Assert.Equal([65, 66, 67], frame);
    }

    [Fact]
    public void DrainFramesExtractsCompleteFramesAndLeavesTrailingPartial()
    {
        var buffer = new SocketReceiveBuffer();

        buffer.Append([0, 1, 65, 0, 2, 66, 67, 0, 2, 68]);

        var frames = buffer.DrainFrames();

        Assert.Collection(
            frames,
            frame => Assert.Equal([65], frame),
            frame => Assert.Equal([66, 67], frame));

        buffer.Append([69]);
        var finalFrame = Assert.Single(buffer.DrainFrames());
        Assert.Equal([68, 69], finalFrame);
    }
}

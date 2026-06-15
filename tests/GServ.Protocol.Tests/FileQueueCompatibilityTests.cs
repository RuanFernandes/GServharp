using System.Text;
using GServ.Protocol;
using Xunit;

namespace GServ.Protocol.Tests;

public sealed class FileQueueCompatibilityTests
{
    [Fact]
    public void Gen1FlushSendsNormalNewlinePacketsUncompressedInQueueOrder()
    {
        var queue = new GraalFileQueue();

        queue.AddPacket(Encoding.ASCII.GetBytes("abc\nxyz\n"));

        Assert.Equal(
            Encoding.ASCII.GetBytes("abc\nxyz\n"),
            queue.FlushUncompressed());
    }

    [Fact]
    public void RawDataHeaderAndBoardPayloadStayInNormalQueue()
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.RawData);
        writer.WriteGInt(2);
        writer.WriteByte((byte)'\n');
        writer.WriteGChar((byte)ServerToPlayerPacketId.BoardPacket);
        writer.WriteByte((byte)'x');

        var queue = new GraalFileQueue();
        queue.AddPacket(writer.ToArray());

        Assert.Equal(writer.ToArray(), queue.FlushUncompressed());
    }

    [Fact]
    public void PartialSocketSendLeavesRemainingOutputBufferedForNextFlush()
    {
        var queue = new GraalFileQueue();
        queue.AddPacket(Encoding.ASCII.GetBytes("abcdef\n"));

        Assert.Equal(Encoding.ASCII.GetBytes("abc"), queue.FlushUncompressed(maxBytes: 3));
        Assert.Equal(Encoding.ASCII.GetBytes("def\n"), queue.FlushUncompressed());
    }
}

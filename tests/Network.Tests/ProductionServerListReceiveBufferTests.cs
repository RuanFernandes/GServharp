using System.IO.Compression;
using GServ.Protocol;
using Xunit;

namespace GServ.Network.Tests;

public sealed class ProductionServerListReceiveBufferTests
{
    [Fact]
    public void DrainPacketsWaitsForCompleteLengthPrefixedZlibFrameAndSplitsNewlinePackets()
    {
        var buffer = new ProductionServerListReceiveBuffer();
        var frame = LengthFrame(Zlib("abc\nxyz\n"u8.ToArray()));

        buffer.Append(frame.AsSpan(0, 3));
        Assert.Empty(buffer.DrainPackets());

        buffer.Append(frame.AsSpan(3));
        Assert.Equal(
            ["abc"u8.ToArray(), "xyz"u8.ToArray()],
            buffer.DrainPackets());
    }

    private static byte[] LengthFrame(byte[] payload) =>
    [
        (byte)(payload.Length >> 8),
        (byte)payload.Length,
        ..payload
    ];

    private static byte[] Zlib(byte[] payload)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(payload);
        return output.ToArray();
    }
}

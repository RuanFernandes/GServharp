using System.Text;
using Preagonal.GServer.Protocol;
using Xunit;

namespace Preagonal.GServer.Protocol.Tests;

public sealed class FileTransferPacketTests
{
    [Fact]
    public void BuildFileNotFoundWritesPloFileSendFailedNameAndNewline()
    {
        Assert.Equal(
            new byte[] { 62, (byte)'m', (byte)'i', (byte)'s', (byte)'s', (byte)'.', (byte)'p', (byte)'n', (byte)'g', 10 },
            FileTransferPackets.FileSendFailed("miss.png"));
    }

    [Fact]
    public void BuildFileUpToDateWritesPloFileUpToDateNameAndNewline()
    {
        Assert.Equal(
            new byte[] { 77, (byte)'h', (byte)'e', (byte)'a', (byte)'d', (byte)'.', (byte)'p', (byte)'n', (byte)'g', 10 },
            FileTransferPackets.FileUpToDate("head.png"));
    }

    [Fact]
    public void BuildModernFileChunkWritesRawDataHeaderAndFilePayloadWithModTime()
    {
        var packet = FileTransferPackets.BuildFileChunk(
            "test.txt",
            Encoding.ASCII.GetBytes("abc"),
            modTime: 1,
            includeModTime: true);

        Assert.Equal(
            new byte[]
            {
                132, 32, 32, 51, 10,
                134, 32, 32, 32, 32, 33, 40,
                (byte)'t', (byte)'e', (byte)'s', (byte)'t', (byte)'.', (byte)'t', (byte)'x', (byte)'t',
                (byte)'a', (byte)'b', (byte)'c', 10
            },
            packet);
    }

    [Fact]
    public void BuildOldClientFileChunkOmitsModTimeAndTrailingNewlineFromRawPayload()
    {
        var packet = FileTransferPackets.BuildFileChunk(
            "test.txt",
            Encoding.ASCII.GetBytes("abc"),
            modTime: 1,
            includeModTime: false);

        Assert.Equal(
            new byte[]
            {
                132, 32, 32, 45, 10,
                134, 40,
                (byte)'t', (byte)'e', (byte)'s', (byte)'t', (byte)'.', (byte)'t', (byte)'x', (byte)'t',
                (byte)'a', (byte)'b', (byte)'c'
            },
            packet);
    }

    [Fact]
    public void BuildLargeFileMarkersMatchCppPacketBodiesWithNewlines()
    {
        Assert.Equal(
            new byte[] { 100, (byte)'b', (byte)'i', (byte)'g', (byte)'.', (byte)'b', (byte)'i', (byte)'n', 10 },
            FileTransferPackets.LargeFileStart("big.bin"));
        Assert.Equal(
            new byte[] { 116, 32, 32, 33, 154, 33, 10 },
            FileTransferPackets.LargeFileSize(32001));
        Assert.Equal(
            new byte[] { 101, (byte)'b', (byte)'i', (byte)'g', (byte)'.', (byte)'b', (byte)'i', (byte)'n', 10 },
            FileTransferPackets.LargeFileEnd("big.bin"));
    }

    [Fact]
    public void BuildUpdatePackageBoundaryPacketsMatchCppOrder()
    {
        Assert.Equal(
            new byte[] { 137, 35, (byte)'p', (byte)'k', (byte)'g', 32, 32, 32, 32, 132, 10 },
            FileTransferPackets.UpdatePackageSize("pkg", 100));
        Assert.Equal(
            new byte[] { 138, (byte)'p', (byte)'k', (byte)'g', 10 },
            FileTransferPackets.UpdatePackageDone("pkg"));
    }
}

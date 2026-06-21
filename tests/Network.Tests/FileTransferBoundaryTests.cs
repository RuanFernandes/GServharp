using System.Text;
using Preagonal.GServer.Network;
using Preagonal.GServer.Protocol;
using Xunit;

namespace Preagonal.GServer.Network.Tests;

public sealed class FileTransferBoundaryTests
{
    [Fact]
    public void WantFileAppendsGifForOldClientsWithoutExtension()
    {
        var fileSystem = new MemoryResourceFileSystem();
        fileSystem.Add("head.gif", Encoding.ASCII.GetBytes("x"), modTime: 1);
        var session = new ClientSessionSkeleton(7);

        var result = FileTransferBoundary.HandleWantFile(
            session,
            fileSystem,
            "head",
            ClientVersionId.Client1411);

        Assert.True(result.Sent);
        Assert.Contains("head.gif", result.KnownFiles);
        Assert.Equal([132, 32, 32, 43, 10, 134, 40, 104, 101, 97, 100, 46, 103, 105, 102, 120], session.TakeOutboundBytes());
    }

    [Fact]
    public void WantFileMissingQueuesFileSendFailedButStillRecordsKnownClientFile()
    {
        var session = new ClientSessionSkeleton(7);

        var result = FileTransferBoundary.HandleWantFile(
            session,
            new MemoryResourceFileSystem(),
            "missing.png",
            ClientVersionId.Client6037);

        Assert.False(result.Sent);
        Assert.Contains("missing.png", result.KnownFiles);
        Assert.Equal([62, 109, 105, 115, 115, 105, 110, 103, 46, 112, 110, 103, 10], session.TakeOutboundBytes());
    }

    [Fact]
    public void VerifyWantSendQueuesUpToDateWhenChecksumMatchesNonGupdFile()
    {
        var fileSystem = new MemoryResourceFileSystem();
        fileSystem.Add("script.txt", Encoding.ASCII.GetBytes("abc"), modTime: 1);
        var session = new ClientSessionSkeleton(7);

        var result = FileTransferBoundary.HandleVerifyWantSend(
            session,
            fileSystem,
            Crc32.Compute(Encoding.ASCII.GetBytes("abc")),
            "script.txt",
            ClientVersionId.Client6037);

        Assert.Equal(FileTransferDecision.UpToDate, result.Decision);
        Assert.Equal([77, 115, 99, 114, 105, 112, 116, 46, 116, 120, 116, 10], session.TakeOutboundBytes());
    }

    [Fact]
    public void VerifyWantSendIgnoresChecksumForGupdAndSendsFile()
    {
        var fileSystem = new MemoryResourceFileSystem();
        fileSystem.Add("pack.gupd", Encoding.ASCII.GetBytes("abc"), modTime: 1);
        var session = new ClientSessionSkeleton(7);

        var result = FileTransferBoundary.HandleVerifyWantSend(
            session,
            fileSystem,
            Crc32.Compute(Encoding.ASCII.GetBytes("abc")),
            "pack.gupd",
            ClientVersionId.Client6037);

        Assert.Equal(FileTransferDecision.SentFile, result.Decision);
        Assert.Equal(2, session.TakeOutboundBytes().Count(value => value == 10));
    }

    [Fact]
    public void UpdateFileQueuesUpToDateWhenModernModTimeMatches()
    {
        var fileSystem = new MemoryResourceFileSystem();
        fileSystem.Add("script.txt", Encoding.ASCII.GetBytes("abc"), modTime: 7);
        var session = new ClientSessionSkeleton(7);

        var result = FileTransferBoundary.HandleUpdateFile(
            session,
            fileSystem,
            clientModTime: 7,
            fileName: "script.txt",
            ClientVersionId.Client6037);

        Assert.Equal(FileTransferDecision.UpToDate, result.Decision);
        Assert.Equal([77, 115, 99, 114, 105, 112, 116, 46, 116, 120, 116, 10], session.TakeOutboundBytes());
    }

    [Fact]
    public void UpdateFileSendsNonDefaultFileWhenModTimeDiffers()
    {
        var fileSystem = new MemoryResourceFileSystem();
        fileSystem.Add("script.txt", Encoding.ASCII.GetBytes("abc"), modTime: 8);
        var session = new ClientSessionSkeleton(7);

        var result = FileTransferBoundary.HandleUpdateFile(
            session,
            fileSystem,
            clientModTime: 7,
            fileName: "script.txt",
            ClientVersionId.Client6037);

        Assert.Equal(FileTransferDecision.SentFile, result.Decision);
        Assert.Equal(2, session.TakeOutboundBytes().Count(value => value == 10));
    }

    [Fact]
    public void UpdateFileDoesNotSendModernDefaultFileEvenWhenModTimeDiffers()
    {
        var fileSystem = new MemoryResourceFileSystem();
        fileSystem.Add("walk.gani", Encoding.ASCII.GetBytes("abc"), modTime: 8);
        var session = new ClientSessionSkeleton(7);

        var result = FileTransferBoundary.HandleUpdateFile(
            session,
            fileSystem,
            clientModTime: 7,
            fileName: "walk.gani",
            ClientVersionId.Client6037);

        Assert.Equal(FileTransferDecision.UpToDate, result.Decision);
        Assert.Equal([77, 119, 97, 108, 107, 46, 103, 97, 110, 105, 10], session.TakeOutboundBytes());
    }

    [Fact]
    public void UpdateFileOldClientAppendsGifBeforeDefaultCheckAndSendsFailedForDefault()
    {
        var fileSystem = new MemoryResourceFileSystem();
        fileSystem.Add("sword1.gif", Encoding.ASCII.GetBytes("abc"), modTime: 8);
        var session = new ClientSessionSkeleton(7);

        var result = FileTransferBoundary.HandleUpdateFile(
            session,
            fileSystem,
            clientModTime: 7,
            fileName: "sword1",
            ClientVersionId.Client1411);

        Assert.Equal(FileTransferDecision.FileMissing, result.Decision);
        Assert.Equal([62, 115, 119, 111, 114, 100, 49, 46, 103, 105, 102, 10], session.TakeOutboundBytes());
    }

    [Fact]
    public void UpdatePackageRequestSendsOnlyFilesWithMissingOrDifferentChecksums()
    {
        var fileSystem = new MemoryResourceFileSystem();
        fileSystem.Add("a.txt", Encoding.ASCII.GetBytes("A"), modTime: 1);
        fileSystem.Add("b.txt", Encoding.ASCII.GetBytes("BC"), modTime: 2);
        var package = new UpdatePackageSnapshot(
            "pkg",
            [
                new UpdatePackageFileEntry("a.txt", Size: 1, Checksum: Crc32.Compute(Encoding.ASCII.GetBytes("A"))),
                new UpdatePackageFileEntry("b.txt", Size: 2, Checksum: Crc32.Compute(Encoding.ASCII.GetBytes("BC")))
            ]);
        var session = new ClientSessionSkeleton(7);

        var result = FileTransferBoundary.HandleUpdatePackageRequest(
            session,
            fileSystem,
            package,
            installType: 1,
            clientChecksums: [Crc32.Compute(Encoding.ASCII.GetBytes("A")), 0],
            ClientVersionId.Client6037);

        Assert.Equal(2, result.TotalDownloadSize);
        Assert.Equal(["b.txt"], result.MissingFiles);
        Assert.Equal(
            [
                137, 35, 112, 107, 103, 32, 32, 32, 32, 34, 10,
                132, 32, 32, 47, 10, 134, 32, 32, 32, 32, 34, 37, 98, 46, 116, 120, 116, 66, 67, 10,
                138, 112, 107, 103, 10
            ],
            session.TakeOutboundBytes());
    }

    [Fact]
    public void UpdatePackageReinstallClearsClientChecksumsAndSendsEveryPackageFile()
    {
        var fileSystem = new MemoryResourceFileSystem();
        fileSystem.Add("a.txt", Encoding.ASCII.GetBytes("A"), modTime: 1);
        fileSystem.Add("b.txt", Encoding.ASCII.GetBytes("B"), modTime: 1);
        var package = new UpdatePackageSnapshot(
            "pkg",
            [
                new UpdatePackageFileEntry("a.txt", Size: 1, Checksum: Crc32.Compute(Encoding.ASCII.GetBytes("A"))),
                new UpdatePackageFileEntry("b.txt", Size: 1, Checksum: Crc32.Compute(Encoding.ASCII.GetBytes("B")))
            ]);
        var session = new ClientSessionSkeleton(7);

        var result = FileTransferBoundary.HandleUpdatePackageRequest(
            session,
            fileSystem,
            package,
            installType: 2,
            clientChecksums: [Crc32.Compute(Encoding.ASCII.GetBytes("A")), Crc32.Compute(Encoding.ASCII.GetBytes("B"))],
            ClientVersionId.Client6037);

        Assert.Equal(2, result.TotalDownloadSize);
        Assert.Equal(["a.txt", "b.txt"], result.MissingFiles);
    }

    private sealed class MemoryResourceFileSystem : IResourceFileSystem
    {
        private readonly Dictionary<string, ResourceFile> _files = new(StringComparer.Ordinal);

        public void Add(string name, byte[] data, long modTime) =>
            _files[name] = new ResourceFile(name, data, modTime);

        public ResourceFile? Find(string file) =>
            _files.GetValueOrDefault(file);
    }
}

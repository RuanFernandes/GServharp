using Preagonal.GServer.Protocol;

namespace Preagonal.GServer.Network;

public sealed record ResourceFile(string Name, byte[] Data, long ModTime);

public interface IResourceFileSystem
{
    ResourceFile? Find(string file);
}

public enum FileTransferDecision
{
    SentFile,
    FileMissing,
    UpToDate
}

public sealed record FileTransferResult(
    FileTransferDecision Decision,
    bool Sent,
    IReadOnlyList<string> KnownFiles);

public sealed record UpdatePackageFileEntry(string FileName, int Size, uint Checksum);

public sealed record UpdatePackageSnapshot(string PackageName, IReadOnlyList<UpdatePackageFileEntry> Files);

public sealed record UpdatePackageTransferResult(int TotalDownloadSize, IReadOnlyList<string> MissingFiles);

public static class FileTransferBoundary
{
    private static readonly string[] DefaultFiles =
    [
        "carried.gani", "carry.gani", "carrystill.gani", "carrypeople.gani", "dead.gani", "def.gani", "ghostani.gani",
        "grab.gani", "gralats.gani", "hatoff.gani", "haton.gani", "hidden.gani", "hiddenstill.gani", "hurt.gani",
        "idle.gani", "kick.gani", "lava.gani", "lift.gani", "maps1.gani", "maps2.gani", "maps3.gani", "pull.gani",
        "push.gani", "ride.gani", "rideeat.gani", "ridefire.gani", "ridehurt.gani", "ridejump.gani", "ridestill.gani",
        "ridesword.gani", "shoot.gani", "sit.gani", "skip.gani", "sleep.gani", "spin.gani", "swim.gani", "sword.gani",
        "walk.gani", "walkslow.gani", "sword?.png", "sword?.gif", "shield?.png", "shield?.gif", "body.png",
        "body2.png", "body3.png", "arrow.wav", "arrowon.wav", "axe.wav", "bomb.wav", "chest.wav", "compudead.wav",
        "crush.wav", "dead.wav", "extra.wav", "fire.wav", "frog.wav", "frog2.wav", "goal.wav", "horse.wav",
        "horse2.wav", "item.wav", "item2.wav", "jump.wav", "lift.wav", "lift2.wav", "nextpage.wav", "put.wav",
        "sign.wav", "steps.wav", "steps2.wav", "stonemove.wav", "sword.wav", "swordon.wav", "thunder.wav",
        "water.wav", "pics1.png"
    ];

    public static FileTransferResult HandleWantFile(
        ClientSessionSkeleton session,
        IResourceFileSystem fileSystem,
        string fileName,
        ClientVersionId clientVersion)
    {
        fileName = NormalizeOldClientFilename(fileName, clientVersion);
        return SendFile(session, fileSystem, fileName, clientVersion);
    }

    public static FileTransferResult HandleVerifyWantSend(
        ClientSessionSkeleton session,
        IResourceFileSystem fileSystem,
        uint clientChecksum,
        string fileName,
        ClientVersionId clientVersion)
    {
        var file = fileSystem.Find(fileName);
        var ignoreChecksum = Path.GetExtension(fileName) == ".gupd";
        if (!ignoreChecksum && file is not null && Crc32.Compute(file.Data) == clientChecksum)
        {
            session.QueuePacket(FileTransferPackets.FileUpToDate(fileName));
            return new FileTransferResult(FileTransferDecision.UpToDate, Sent: false, KnownFiles: []);
        }

        return SendFile(session, fileSystem, fileName, clientVersion);
    }

    public static FileTransferResult HandleUpdateFile(
        ClientSessionSkeleton session,
        IResourceFileSystem fileSystem,
        long clientModTime,
        string fileName,
        ClientVersionId clientVersion)
    {
        var serverModTime = fileSystem.Find(fileName)?.ModTime ?? 0;

        if (clientVersion < ClientVersionId.Client21 && Path.GetExtension(fileName).Length == 0)
            fileName += ".gif";

        if (!IsDefaultFile(fileName) && clientModTime != serverModTime)
            return SendFile(session, fileSystem, fileName, clientVersion);

        if (clientVersion < ClientVersionId.Client21)
        {
            session.QueuePacket(FileTransferPackets.FileSendFailed(fileName));
            return new FileTransferResult(FileTransferDecision.FileMissing, Sent: false, KnownFiles: []);
        }

        session.QueuePacket(FileTransferPackets.FileUpToDate(fileName));
        return new FileTransferResult(FileTransferDecision.UpToDate, Sent: false, KnownFiles: []);
    }

    public static UpdatePackageTransferResult HandleUpdatePackageRequest(
        ClientSessionSkeleton session,
        IResourceFileSystem fileSystem,
        UpdatePackageSnapshot package,
        byte installType,
        IReadOnlyList<uint> clientChecksums,
        ClientVersionId clientVersion)
    {
        var useClientChecksums = installType != 2;
        var missingFiles = new List<string>();
        var totalDownloadSize = 0;

        for (var i = 0; i < package.Files.Count; i++)
        {
            var entry = package.Files[i];
            var needsFile = true;
            if (useClientChecksums && i < clientChecksums.Count && entry.Checksum == clientChecksums[i])
                needsFile = false;

            if (!needsFile)
                continue;

            totalDownloadSize += entry.Size;
            missingFiles.Add(entry.FileName);
        }

        session.QueuePacket(FileTransferPackets.UpdatePackageSize(package.PackageName, totalDownloadSize));

        foreach (var fileName in missingFiles)
            SendFile(session, fileSystem, fileName, clientVersion);

        session.QueuePacket(FileTransferPackets.UpdatePackageDone(package.PackageName));

        return new UpdatePackageTransferResult(totalDownloadSize, missingFiles);
    }

    private static FileTransferResult SendFile(
        ClientSessionSkeleton session,
        IResourceFileSystem fileSystem,
        string fileName,
        ClientVersionId clientVersion)
    {
        var knownFiles = new List<string> { fileName };
        var file = fileSystem.Find(fileName);
        if (file is null || file.Data.Length == 0)
        {
            session.QueuePacket(FileTransferPackets.FileSendFailed(fileName));
            return new FileTransferResult(FileTransferDecision.FileMissing, Sent: false, knownFiles);
        }

        var includeModTime = clientVersion >= ClientVersionId.Client21;
        var isBigFile = file.Data.Length > FileTransferPackets.ChunkSize;
        if (clientVersion < ClientVersionId.Client214)
        {
            if (file.Data.Length > 64000)
            {
                session.QueuePacket(FileTransferPackets.FileSendFailed(fileName));
                return new FileTransferResult(FileTransferDecision.FileMissing, Sent: false, knownFiles);
            }

            isBigFile = false;
        }

        if (isBigFile)
        {
            session.QueuePacket(FileTransferPackets.LargeFileStart(fileName));
            session.QueuePacket(FileTransferPackets.LargeFileSize(file.Data.Length));
        }

        var offset = 0;
        while (offset < file.Data.Length)
        {
            var sendSize = Math.Min(FileTransferPackets.ChunkSize, file.Data.Length - offset);
            if (clientVersion < ClientVersionId.Client214)
                sendSize = file.Data.Length - offset;

            session.QueuePacket(FileTransferPackets.BuildFileChunk(
                fileName,
                file.Data.AsSpan(offset, sendSize),
                file.ModTime,
                includeModTime));
            offset += sendSize;
        }

        if (isBigFile)
            session.QueuePacket(FileTransferPackets.LargeFileEnd(fileName));

        return new FileTransferResult(FileTransferDecision.SentFile, Sent: true, knownFiles);
    }

    private static string NormalizeOldClientFilename(string fileName, ClientVersionId clientVersion)
    {
        if (clientVersion < ClientVersionId.Client21 && Path.GetExtension(fileName).Length == 0)
            return fileName + ".gif";
        return fileName;
    }

    private static bool IsDefaultFile(string fileName) =>
        DefaultFiles.Any(pattern => MatchesDefaultPattern(fileName, pattern));

    private static bool MatchesDefaultPattern(string value, string pattern)
    {
        if (value.Length != pattern.Length)
            return false;

        for (var i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] != '?' && pattern[i] != value[i])
                return false;
        }

        return true;
    }
}

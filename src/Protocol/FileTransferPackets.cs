using System.Text;

namespace Preagonal.GServer.Protocol;

public static class FileTransferPackets
{
    public const int ChunkSize = 32000;

    public static byte[] FileSendFailed(string fileName) =>
        BuildTextPacket(ServerToPlayerPacketId.FileSendFailed, fileName);

    public static byte[] FileUpToDate(string fileName) =>
        BuildTextPacket(ServerToPlayerPacketId.FileUpToDate, fileName);

    public static byte[] LargeFileStart(string fileName) =>
        BuildTextPacket(ServerToPlayerPacketId.LargeFileStart, fileName);

    public static byte[] LargeFileEnd(string fileName) =>
        BuildTextPacket(ServerToPlayerPacketId.LargeFileEnd, fileName);

    public static byte[] LargeFileSize(int size)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.LargeFileSize);
        writer.WriteGInt5(unchecked((uint)size));
        writer.WriteByte((byte)'\n');
        return writer.ToArray();
    }

    public static byte[] UpdatePackageSize(string packageName, int totalDownloadSize)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.UpdatePackageSize);
        writer.WriteGChar((byte)packageName.Length);
        writer.WriteBytes(Encoding.ASCII.GetBytes(packageName));
        writer.WriteGInt5(unchecked((uint)totalDownloadSize));
        writer.WriteByte((byte)'\n');
        return writer.ToArray();
    }

    public static byte[] UpdatePackageDone(string packageName) =>
        BuildTextPacket(ServerToPlayerPacketId.UpdatePackageDone, packageName);

    public static byte[] UpdatePackageIsUpdated(string fileName) =>
        BuildTextPacket(ServerToPlayerPacketId.UpdatePackageIsUpdated, fileName);

    public static byte[] BuildFileChunk(
        string fileName,
        ReadOnlySpan<byte> chunk,
        long modTime,
        bool includeModTime)
    {
        var packetLength = 1 + 5 + 1 + fileName.Length + 1;
        var rawLength = includeModTime
            ? packetLength + chunk.Length
            : packetLength - 1 - 5 + chunk.Length;

        var writer = new GraalBinaryWriter();
        writer.WriteBytes(RawDataHeader(rawLength));
        writer.WriteGChar((byte)ServerToPlayerPacketId.File);
        if (includeModTime)
            writer.WriteGInt5(unchecked((uint)modTime));
        writer.WriteGChar((byte)fileName.Length);
        writer.WriteBytes(Encoding.ASCII.GetBytes(fileName));
        writer.WriteBytes(chunk);
        if (includeModTime)
            writer.WriteByte((byte)'\n');

        return writer.ToArray();
    }

    public static byte[] RawDataHeader(int length)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ServerToPlayerPacketId.RawData);
        writer.WriteGInt(unchecked((uint)length));
        writer.WriteByte((byte)'\n');
        return writer.ToArray();
    }

    private static byte[] BuildTextPacket(ServerToPlayerPacketId packetId, string text)
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)packetId);
        writer.WriteBytes(Encoding.ASCII.GetBytes(text));
        writer.WriteByte((byte)'\n');
        return writer.ToArray();
    }
}

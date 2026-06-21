namespace Preagonal.GServer.Protocol;

public readonly record struct FramedPacket(PlayerToServerPacketId? Id, ReadOnlyMemory<byte> Payload);
public readonly record struct ClientPacketParseOptions(bool StripRawDataTrailingNewline);

public sealed class ClientPacketStreamFramer(ClientPacketParseOptions options)
{
    private int _nextRawSize = -1;

    public IReadOnlyList<FramedPacket> Parse(ReadOnlySpan<byte> payload)
    {
        var packets = new List<FramedPacket>();
        var reader = new GraalBinaryReader(payload);

        while (reader.BytesLeft > 0)
        {
            if (_nextRawSize >= 0)
            {
                var raw = reader.ReadBytes(_nextRawSize);
                if (options.StripRawDataTrailingNewline && raw.Length > 0 && raw[^1] == (byte)'\n')
                    raw = raw[..^1];
                packets.Add(new FramedPacket(null, raw));
                _nextRawSize = -1;
                continue;
            }

            var line = PacketFramer.ReadLine(reader);
            if (line.Length == 0) continue;
            AddLinePackets(line, packets, size => _nextRawSize = size);
        }

        return packets;
    }

    private static void AddLinePackets(
        ReadOnlySpan<byte> line,
        List<FramedPacket> packets,
        Action<int> setNextRawSize)
    {
        var lineReader = new GraalBinaryReader(line);
        var id = (PlayerToServerPacketId)lineReader.ReadGChar();
        if (id == PlayerToServerPacketId.Bundle)
        {
            foreach (var inner in PacketFramer.ReadBundle(line[1..]))
                AddLinePackets(inner.Span, packets, setNextRawSize);
            return;
        }

        packets.Add(new FramedPacket(id, line.ToArray()));
        if (id == PlayerToServerPacketId.RawData)
            setNextRawSize(lineReader.ReadGInt());
    }
}

public static class PacketFramer
{
    public static IReadOnlyList<FramedPacket> SplitNewlinePackets(ReadOnlySpan<byte> payload)
    {
        var packets = new List<FramedPacket>();
        var start = 0;
        for (var i = 0; i < payload.Length; i++)
        {
            if (payload[i] != (byte)'\n') continue;
            packets.Add(new FramedPacket(null, payload[start..i].ToArray()));
            start = i + 1;
        }

        if (start < payload.Length)
            packets.Add(new FramedPacket(null, payload[start..].ToArray()));

        return packets;
    }

    public static IReadOnlyList<FramedPacket> ParseClientPackets(ReadOnlySpan<byte> payload) =>
        ParseClientPackets(payload, new ClientPacketParseOptions(false));

    public static IReadOnlyList<FramedPacket> ParseClientPackets(ReadOnlySpan<byte> payload, ClientPacketParseOptions options)
    {
        var packets = new List<FramedPacket>();
        var reader = new GraalBinaryReader(payload);
        var nextRawSize = -1;

        while (reader.BytesLeft > 0)
        {
            if (nextRawSize >= 0)
            {
                var raw = reader.ReadBytes(nextRawSize);
                if (options.StripRawDataTrailingNewline && raw.Length > 0 && raw[^1] == (byte)'\n')
                    raw = raw[..^1];
                packets.Add(new FramedPacket(null, raw));
                nextRawSize = -1;
                continue;
            }

            var line = ReadLine(reader);
            if (line.Length == 0) continue;
            AddLinePackets(line, packets, size => nextRawSize = size);
        }

        return packets;
    }

    private static void AddLinePackets(
        ReadOnlySpan<byte> line,
        List<FramedPacket> packets,
        Action<int> setNextRawSize)
    {
        var lineReader = new GraalBinaryReader(line);
        var id = (PlayerToServerPacketId)lineReader.ReadGChar();
        if (id == PlayerToServerPacketId.Bundle)
        {
            foreach (var inner in ReadBundle(line[1..]))
                AddLinePackets(inner.Span, packets, setNextRawSize);
            return;
        }

        packets.Add(new FramedPacket(id, line.ToArray()));
        if (id == PlayerToServerPacketId.RawData)
            setNextRawSize(lineReader.ReadGInt());
    }

    public static IReadOnlyList<ReadOnlyMemory<byte>> ReadLengthPrefixedFrames(ReadOnlySpan<byte> payload)
    {
        var frames = new List<ReadOnlyMemory<byte>>();
        var offset = 0;
        while (payload.Length - offset > 1)
        {
            var length = (payload[offset] << 8) | payload[offset + 1];
            if (length > payload.Length - offset - 2)
                break;

            frames.Add(payload.Slice(offset + 2, length).ToArray());
            offset += length + 2;
        }

        return frames;
    }

    public static IReadOnlyList<ReadOnlyMemory<byte>> ReadBundle(ReadOnlySpan<byte> payload)
    {
        var packets = new List<ReadOnlyMemory<byte>>();
        var reader = new GraalBinaryReader(payload);
        while (reader.BytesLeft > 0)
        {
            var size = reader.ReadRawUnsignedShort();
            packets.Add(reader.ReadBytes(size));
        }

        return packets;
    }

    internal static byte[] ReadLine(GraalBinaryReader reader)
    {
        var bytes = new List<byte>();
        while (reader.BytesLeft > 0)
        {
            var b = reader.ReadByte();
            if (b == (byte)'\n') break;
            bytes.Add(b);
        }

        return bytes.ToArray();
    }
}

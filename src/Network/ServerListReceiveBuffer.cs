using System.IO.Compression;

namespace Preagonal.GServer.Network;

public sealed class ServerListReceiveBuffer
{
    private readonly List<byte> _frameBuffer = [];
    private readonly List<byte> _packetBuffer = [];

    public void Append(ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
            _frameBuffer.Add(value);
    }

    public IReadOnlyList<byte[]> DrainPackets()
    {
        DrainFrames();
        var packets = new List<byte[]>();
        while (true)
        {
            var newline = _packetBuffer.IndexOf((byte)'\n');
            if (newline < 0)
                break;

            packets.Add(_packetBuffer.GetRange(0, newline).ToArray());
            _packetBuffer.RemoveRange(0, newline + 1);
        }

        return packets;
    }

    private void DrainFrames()
    {
        while (_frameBuffer.Count > 1)
        {
            var length = (_frameBuffer[0] << 8) | _frameBuffer[1];
            if (length > _frameBuffer.Count - 2)
                return;

            var compressed = _frameBuffer.GetRange(2, length).ToArray();
            _frameBuffer.RemoveRange(0, length + 2);
            _packetBuffer.AddRange(Inflate(compressed));
        }
    }

    private static byte[] Inflate(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        return output.ToArray();
    }
}

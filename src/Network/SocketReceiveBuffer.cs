namespace Preagonal.GServer.Network;

public sealed class SocketReceiveBuffer
{
    private readonly List<byte> _buffer = [];

    public int BufferedByteCount => _buffer.Count;

    public ushort? PendingFrameLength =>
        _buffer.Count < 2 ? null : (ushort)((_buffer[0] << 8) | _buffer[1]);

    public void Append(ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
            _buffer.Add(value);
    }

    public IReadOnlyList<byte[]> DrainFrames()
    {
        var frames = new List<byte[]>();
        while (_buffer.Count > 1)
        {
            var length = (ushort)((_buffer[0] << 8) | _buffer[1]);
            if (length > _buffer.Count - 2)
                break;

            var frame = _buffer.GetRange(2, length).ToArray();
            frames.Add(frame);
            _buffer.RemoveRange(0, length + 2);
        }

        return frames;
    }
}

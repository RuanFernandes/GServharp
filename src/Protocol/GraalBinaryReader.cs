using System.Buffers.Binary;

namespace Preagonal.GServer.Protocol;

public sealed class GraalBinaryReader
{
    private readonly byte[] _buffer;
    private int _position;

    public GraalBinaryReader(ReadOnlySpan<byte> buffer)
    {
        _buffer = buffer.ToArray();
    }

    public int BytesLeft => Math.Max(0, _buffer.Length - _position);

    public byte ReadByte()
    {
        if (_position >= _buffer.Length) return 0;
        return _buffer[_position++];
    }

    public byte[] ReadBytes(int length)
    {
        length = Math.Clamp(length, 0, BytesLeft);
        var bytes = _buffer.AsSpan(_position, length).ToArray();
        _position += length;
        return bytes;
    }

    public ushort ReadRawUnsignedShort()
    {
        var bytes = ReadBytes(2);
        return bytes.Length < 2 ? (ushort)0 : BinaryPrimitives.ReadUInt16BigEndian(bytes);
    }

    public byte ReadGChar() => unchecked((byte)(ReadByte() - 32));

    public ushort ReadGShort()
    {
        var a = ReadByte();
        var b = ReadByte();
        return (ushort)((a << 7) + b - 0x1020);
    }

    public int ReadGInt()
    {
        var a = ReadByte();
        var b = ReadByte();
        var c = ReadByte();
        return (((a << 7) + b) << 7) + c - 0x81020;
    }

    public uint ReadGUInt() => unchecked((uint)ReadGInt());

    public int ReadGInt4()
    {
        var a = ReadByte();
        var b = ReadByte();
        var c = ReadByte();
        var d = ReadByte();
        return (((((a << 7) + b) << 7) + c) << 7) + d - 0x4081020;
    }

    public uint ReadGInt5()
    {
        var a = (uint)ReadByte();
        var b = ReadByte();
        var c = ReadByte();
        var d = ReadByte();
        var e = ReadByte();
        return (((((((a << 7) + b) << 7) + c) << 7) + d) << 7) + e - 0x4081020u;
    }
}

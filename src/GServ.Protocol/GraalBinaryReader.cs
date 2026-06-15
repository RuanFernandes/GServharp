using System.Text;
using GServ.Core.Compatibility;

namespace GServ.Protocol;

/// <summary>
/// Reads Graal protocol primitive values.
/// Source mapping: gs2lib CString::readG* at commit 63b1ae96491c188905b50c6b61c8532c601a2122.
/// </summary>
public sealed class GraalBinaryReader
{
    private static readonly Encoding WireEncoding = Encoding.Latin1;
    private readonly ReadOnlyMemory<byte> _buffer;
    private int _position;

    public GraalBinaryReader(ReadOnlyMemory<byte> buffer)
    {
        _buffer = buffer;
    }

    public int BytesLeft => _buffer.Length - _position;
    public bool IsEmpty => BytesLeft == 0;

    public byte ReadByte()
    {
        EnsureAvailable(1);
        return _buffer.Span[_position++];
    }

    public sbyte ReadGChar()
    {
        return unchecked((sbyte)(ReadByte() - CompatibilityConstants.GraalAsciiOffset));
    }

    public byte ReadGUChar() => unchecked((byte)ReadGChar());

    public short ReadGShort()
    {
        var b0 = ReadGUChar();
        var b1 = ReadGUChar();
        return (short)((b0 << 7) + b1);
    }

    public ushort ReadGUShort() => unchecked((ushort)ReadGShort());

    public int ReadGInt()
    {
        var b0 = ReadGUChar();
        var b1 = ReadGUChar();
        var b2 = ReadGUChar();
        return (b0 << 14) + (b1 << 7) + b2;
    }

    public uint ReadGUInt() => unchecked((uint)ReadGInt());

    public int ReadGInt4()
    {
        var b0 = ReadByte();
        var b1 = ReadByte();
        var b2 = ReadByte();
        var b3 = ReadByte();
        return (((((b0 << 7) + b1) << 7) + b2) << 7) + b3 - 0x4081020;
    }

    public uint ReadGUInt5()
    {
        var b0 = ReadGUChar();
        var b1 = ReadGUChar();
        var b2 = ReadGUChar();
        var b3 = ReadGUChar();
        var b4 = ReadGUChar();
        return (uint)((b0 << 28) + (b1 << 21) + (b2 << 14) + (b3 << 7) + b4);
    }

    public string ReadLengthPrefixedString()
    {
        var length = ReadGUChar();
        return ReadFixedString(length);
    }

    public string ReadFixedString(int length)
    {
        EnsureAvailable(length);
        var value = WireEncoding.GetString(_buffer.Span.Slice(_position, length));
        _position += length;
        return value;
    }

    public string ReadRemainingString()
    {
        var value = WireEncoding.GetString(_buffer.Span[_position..]);
        _position = _buffer.Length;
        return value;
    }

    private void EnsureAvailable(int count)
    {
        if (BytesLeft < count)
        {
            throw new InvalidOperationException($"The buffer does not contain {count} readable byte(s).");
        }
    }
}

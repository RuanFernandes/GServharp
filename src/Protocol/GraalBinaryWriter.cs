using System.Buffers.Binary;

namespace Preagonal.GServer.Protocol;

public sealed class GraalBinaryWriter
{
    private readonly MemoryStream _stream = new();

    public void WriteByte(byte value) => _stream.WriteByte(value);

    public void WriteBytes(ReadOnlySpan<byte> value) => _stream.Write(value);

    public void WriteRawShort(short value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteInt16BigEndian(bytes, value);
        _stream.Write(bytes);
    }

    public void WriteRawShort(ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        _stream.Write(bytes);
    }

    public void WriteRawInt(int value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        _stream.Write(bytes);
    }

    public void WriteGChar(byte value)
    {
        _stream.WriteByte((byte)(Math.Min(value, (byte)223) + 32));
    }

    public void WriteGShort(ushort value)
    {
        var t = Math.Min(value, (ushort)28767);
        var first = (byte)(t >> 7);
        if (first > 223) first = 223;
        var second = (byte)(t - (first << 7));
        _stream.WriteByte((byte)(first + 32));
        _stream.WriteByte((byte)(second + 32));
    }

    public void WriteGInt(uint value)
    {
        var t = Math.Min(value, 3682399u);
        var first = (byte)(t >> 14);
        if (first > 223) first = 223;
        t -= (uint)first << 14;
        var second = (byte)(t >> 7);
        if (second > 223) second = 223;
        var third = (byte)(t - ((uint)second << 7));
        _stream.WriteByte((byte)(first + 32));
        _stream.WriteByte((byte)(second + 32));
        _stream.WriteByte((byte)(third + 32));
    }

    public void WriteGInt4(uint value)
    {
        var t = Math.Min(value, 471347295u);
        var first = (byte)(t >> 21);
        if (first > 223) first = 223;
        t -= (uint)first << 21;
        var second = (byte)(t >> 14);
        if (second > 223) second = 223;
        t -= (uint)second << 14;
        var third = (byte)(t >> 7);
        if (third > 223) third = 223;
        var fourth = (byte)(t - ((uint)third << 7));
        _stream.WriteByte((byte)(first + 32));
        _stream.WriteByte((byte)(second + 32));
        _stream.WriteByte((byte)(third + 32));
        _stream.WriteByte((byte)(fourth + 32));
    }

    public void WriteGInt5(uint value)
    {
        var t = value;
        var first = (byte)((t >> 28) & 0x0F);
        t -= (uint)first << 28;
        var second = (byte)((t >> 21) & 0x7F);
        t -= (uint)second << 21;
        var third = (byte)((t >> 14) & 0x7F);
        t -= (uint)third << 14;
        var fourth = (byte)((t >> 7) & 0x7F);
        var fifth = (byte)((t - ((uint)fourth << 7)) & 0x7F);
        _stream.WriteByte((byte)(first + 32));
        _stream.WriteByte((byte)(second + 32));
        _stream.WriteByte((byte)(third + 32));
        _stream.WriteByte((byte)(fourth + 32));
        _stream.WriteByte((byte)(fifth + 32));
    }

    public byte[] ToArray() => _stream.ToArray();
}

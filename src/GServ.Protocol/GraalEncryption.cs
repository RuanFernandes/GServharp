namespace GServ.Protocol;

public enum GraalEncryptionGeneration : uint
{
    Gen1 = 0,
    Gen2 = 1,
    Gen3 = 2,
    Gen4 = 3,
    Gen5 = 4,
    Gen6 = 5,
}

public enum GraalCompressionType : byte
{
    Uncompressed = 0x02,
    Zlib = 0x04,
    Bz2 = 0x06,
}

/// <summary>
/// Legacy Graal packet encryption primitive.
/// Source mapping: gs2lib CEncryption at commit 63b1ae96491c188905b50c6b61c8532c601a2122.
/// This class only implements the primitive; it is not wired into login/session flow yet.
/// </summary>
public sealed class GraalEncryption
{
    public static readonly uint[] IteratorStart = [0, 0, 0x04A80B38, 0x04A80B38, 0x04A80B38, 0];

    private byte _key;
    private uint _iterator;
    private int _limit;
    private GraalEncryptionGeneration _generation;

    public GraalEncryption()
    {
        _limit = -1;
        _generation = GraalEncryptionGeneration.Gen3;
        _iterator = IteratorStart[(int)_generation];
    }

    public GraalEncryptionGeneration Generation
    {
        get => _generation;
        set
        {
            _generation = value > GraalEncryptionGeneration.Gen6 ? GraalEncryptionGeneration.Gen6 : value;
            _iterator = IteratorStart[(int)_generation];
        }
    }

    public void Reset(byte key)
    {
        _key = key;
        _iterator = IteratorStart[(int)_generation];
        _limit = -1;
    }

    public byte[] Encrypt(ReadOnlySpan<byte> buffer)
    {
        if (buffer.IsEmpty)
        {
            return [];
        }

        return _generation switch
        {
            GraalEncryptionGeneration.Gen1 or GraalEncryptionGeneration.Gen2 or GraalEncryptionGeneration.Gen6 => buffer.ToArray(),
            GraalEncryptionGeneration.Gen3 => EncryptGen3(buffer),
            GraalEncryptionGeneration.Gen4 or GraalEncryptionGeneration.Gen5 => XorGen4Or5(buffer),
            _ => buffer.ToArray(),
        };
    }

    public byte[] Decrypt(ReadOnlySpan<byte> buffer)
    {
        if (buffer.IsEmpty)
        {
            return [];
        }

        return _generation switch
        {
            GraalEncryptionGeneration.Gen1 or GraalEncryptionGeneration.Gen2 or GraalEncryptionGeneration.Gen6 => buffer.ToArray(),
            GraalEncryptionGeneration.Gen3 => DecryptGen3(buffer),
            GraalEncryptionGeneration.Gen4 or GraalEncryptionGeneration.Gen5 => XorGen4Or5(buffer),
            _ => buffer.ToArray(),
        };
    }

    public void Limit(int limit) => _limit = limit;

    public bool LimitFromType(GraalCompressionType type)
    {
        _limit = type switch
        {
            GraalCompressionType.Uncompressed => 0x0C,
            GraalCompressionType.Zlib => 0x04,
            GraalCompressionType.Bz2 => 0x04,
            _ => _limit,
        };

        return type is GraalCompressionType.Uncompressed or GraalCompressionType.Zlib or GraalCompressionType.Bz2;
    }

    private byte[] EncryptGen3(ReadOnlySpan<byte> buffer)
    {
        AdvanceIterator();
        var pos = (int)((_iterator & 0x0FFFF) % (uint)buffer.Length);
        var output = new byte[buffer.Length + 1];
        buffer[..pos].CopyTo(output);
        output[pos] = (byte)')';
        buffer[pos..].CopyTo(output.AsSpan(pos + 1));
        return output;
    }

    private byte[] DecryptGen3(ReadOnlySpan<byte> buffer)
    {
        AdvanceIterator();
        var pos = (int)((_iterator & 0x0FFFF) % (uint)buffer.Length);
        var output = new byte[buffer.Length - 1];
        buffer[..pos].CopyTo(output);
        buffer[(pos + 1)..].CopyTo(output.AsSpan(pos));
        return output;
    }

    private byte[] XorGen4Or5(ReadOnlySpan<byte> buffer)
    {
        var output = buffer.ToArray();

        for (var i = 0; i < output.Length; ++i)
        {
            if (i % 4 == 0)
            {
                if (_limit == 0)
                {
                    return output;
                }

                AdvanceIterator();
                if (_limit > 0)
                {
                    _limit--;
                }
            }

            output[i] ^= (byte)(_iterator >> ((i % 4) * 8));
        }

        return output;
    }

    private void AdvanceIterator()
    {
        _iterator = unchecked((_iterator * 0x08088405) + _key);
    }
}

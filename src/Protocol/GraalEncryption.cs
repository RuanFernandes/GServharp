namespace Preagonal.GServer.Protocol;

public sealed class GraalEncryption
{
    private static readonly uint[] IteratorStart = [0, 0, 0x04A80B38, 0x04A80B38, 0x04A80B38, 0];
    private readonly EncryptionGeneration _generation;
    private byte _key;
    private uint _iterator;
    private int _limit = -1;

    public GraalEncryption(EncryptionGeneration generation)
    {
        _generation = generation;
        _iterator = IteratorStart[(int)generation];
    }

    public void Reset(byte key)
    {
        _key = key;
        _iterator = IteratorStart[(int)_generation];
        _limit = -1;
    }

    public void Limit(int limit) => _limit = limit;

    public bool LimitFromCompressionType(CompressionType type)
    {
        _limit = type switch
        {
            CompressionType.Uncompressed => 0x0C,
            CompressionType.Zlib => 0x04,
            CompressionType.Bz2 => 0x04,
            _ => _limit
        };
        return type is CompressionType.Uncompressed or CompressionType.Zlib or CompressionType.Bz2;
    }

    public byte[] Encrypt(ReadOnlySpan<byte> payload)
    {
        if (payload.IsEmpty) return [];

        return _generation switch
        {
            EncryptionGeneration.Gen1 or EncryptionGeneration.Gen2 or EncryptionGeneration.Gen6 => payload.ToArray(),
            EncryptionGeneration.Gen3 => EncryptGen3(payload),
            EncryptionGeneration.Gen4 or EncryptionGeneration.Gen5 => XorGen45(payload),
            _ => payload.ToArray()
        };
    }

    public byte[] Decrypt(ReadOnlySpan<byte> payload)
    {
        if (payload.IsEmpty) return [];

        return _generation switch
        {
            EncryptionGeneration.Gen1 or EncryptionGeneration.Gen2 or EncryptionGeneration.Gen6 => payload.ToArray(),
            EncryptionGeneration.Gen3 => DecryptGen3(payload),
            EncryptionGeneration.Gen4 or EncryptionGeneration.Gen5 => XorGen45(payload),
            _ => payload.ToArray()
        };
    }

    private byte[] EncryptGen3(ReadOnlySpan<byte> payload)
    {
        AdvanceIterator();
        var pos = (int)((_iterator & 0x0FFFF) % payload.Length);
        var output = new byte[payload.Length + 1];
        payload[..pos].CopyTo(output);
        output[pos] = (byte)')';
        payload[pos..].CopyTo(output.AsSpan(pos + 1));
        return output;
    }

    private byte[] DecryptGen3(ReadOnlySpan<byte> payload)
    {
        AdvanceIterator();
        var pos = (int)((_iterator & 0x0FFFF) % payload.Length);
        var output = new byte[payload.Length - 1];
        payload[..pos].CopyTo(output);
        payload[(pos + 1)..].CopyTo(output.AsSpan(pos));
        return output;
    }

    private byte[] XorGen45(ReadOnlySpan<byte> payload)
    {
        var output = payload.ToArray();
        for (var i = 0; i < output.Length; i++)
        {
            if (i % 4 == 0)
            {
                if (_limit == 0) return output;
                AdvanceIterator();
                if (_limit > 0) _limit--;
            }

            output[i] ^= (byte)((_iterator >> ((i % 4) * 8)) & 0xFF);
        }

        return output;
    }

    private void AdvanceIterator()
    {
        unchecked
        {
            _iterator = (_iterator * 0x08088405u) + _key;
        }
    }
}

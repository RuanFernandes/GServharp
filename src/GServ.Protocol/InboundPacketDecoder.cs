using System.IO.Compression;
using ICSharpCode.SharpZipLib.BZip2;

namespace GServ.Protocol;

public sealed record InboundFrameDecodeResult(byte[] DecodedPayload, IReadOnlyList<string> Warnings);
public sealed record InboundPacketDecodeResult(byte[] DecodedPayload, IReadOnlyList<byte[]> Packets, IReadOnlyList<string> Warnings);

public sealed class InboundPacketDecoder
{
    private readonly EncryptionGeneration _generation;
    private readonly GraalEncryption _codec;

    public InboundPacketDecoder(EncryptionGeneration generation, byte key)
    {
        _generation = generation;
        _codec = new GraalEncryption(generation);
        _codec.Reset(key);
    }

    public InboundPacketDecodeResult DecodeSocketFramePayload(ReadOnlySpan<byte> framePayload)
    {
        var decodedFrame = DecodeSocketFrame(framePayload);
        var decoded = decodedFrame.DecodedPayload;

        var packets = SplitNewlinePackets(decoded);
        if (_generation == EncryptionGeneration.Gen3)
            packets = packets.Select(packet => _codec.Decrypt(packet)).ToArray();

        return new InboundPacketDecodeResult(decoded, packets, decodedFrame.Warnings);
    }

    public InboundFrameDecodeResult DecodeSocketFrame(ReadOnlySpan<byte> framePayload)
    {
        var warnings = new List<string>();
        var decoded = _generation switch
        {
            EncryptionGeneration.Gen1 or EncryptionGeneration.Gen6 => framePayload.ToArray(),
            EncryptionGeneration.Gen2 => ZlibDecompress(framePayload),
            EncryptionGeneration.Gen3 => ZlibDecompress(framePayload),
            EncryptionGeneration.Gen4 => DecodeGen4(framePayload),
            EncryptionGeneration.Gen5 => DecodeGen5(framePayload, warnings),
            _ => throw new NotSupportedException($"Inbound generation {_generation} is not source-confirmed.")
        };

        return new InboundFrameDecodeResult(decoded, warnings);
    }

    private byte[] DecodeGen4(ReadOnlySpan<byte> framePayload)
    {
        _codec.LimitFromCompressionType(CompressionType.Bz2);
        var decrypted = _codec.Decrypt(framePayload);
        return Bzip2Decompress(decrypted);
    }

    private byte[] DecodeGen5(ReadOnlySpan<byte> framePayload, List<string> warnings)
    {
        if (framePayload.IsEmpty)
            return [];

        var compressionTypeByte = framePayload[0];
        var compressionType = (CompressionType)compressionTypeByte;
        var knownCompressionType = _codec.LimitFromCompressionType(compressionType);

        var decrypted = _codec.Decrypt(framePayload[1..]);
        if (!knownCompressionType)
        {
            warnings.Add($"Client gave incorrect packet compression type 0x{compressionTypeByte:X2}; C++ logs this and continues without decompression.");
            return decrypted;
        }

        return compressionType switch
        {
            CompressionType.Uncompressed => decrypted,
            CompressionType.Zlib => ZlibDecompress(decrypted),
            CompressionType.Bz2 => Bzip2Decompress(decrypted),
            _ => throw new NotSupportedException($"Inbound gen5 compression type 0x{framePayload[0]:X2} is not source-confirmed.")
        };
    }

    private static byte[] ZlibDecompress(ReadOnlySpan<byte> payload)
    {
        using var input = new MemoryStream(payload.ToArray());
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        return output.ToArray();
    }

    private static byte[] Bzip2Decompress(ReadOnlySpan<byte> payload)
    {
        using var input = new MemoryStream(payload.ToArray());
        using var bzip2 = new BZip2InputStream(input);
        using var output = new MemoryStream();
        bzip2.CopyTo(output);
        return output.ToArray();
    }

    private static byte[][] SplitNewlinePackets(byte[] decoded)
    {
        var packets = new List<byte[]>();
        var start = 0;
        for (var i = 0; i < decoded.Length; i++)
        {
            if (decoded[i] != (byte)'\n')
                continue;

            packets.Add(decoded.AsSpan(start, i - start).ToArray());
            start = i + 1;
        }

        if (start < decoded.Length)
            packets.Add(decoded.AsSpan(start).ToArray());

        return packets.ToArray();
    }
}

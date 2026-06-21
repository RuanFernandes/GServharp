using System.Text;
using Preagonal.GServer.Protocol;
using Xunit;

namespace Preagonal.GServer.Protocol.Tests;

public sealed class GraalEncryptionTests
{
    [Fact]
    public void Gen1AndGen2DoNotChangePayload()
    {
        var payload = Encoding.ASCII.GetBytes("hello");

        Assert.Equal(payload, new GraalEncryption(EncryptionGeneration.Gen1).Encrypt(payload));
        Assert.Equal(payload, new GraalEncryption(EncryptionGeneration.Gen2).Encrypt(payload));
    }

    [Fact]
    public void Gen3InsertsRightParenAndDecryptRemovesIt()
    {
        var codec = new GraalEncryption(EncryptionGeneration.Gen3);
        codec.Reset(0);

        var encrypted = codec.Encrypt(Encoding.ASCII.GetBytes("abcd"));

        Assert.Equal(")abcd", Encoding.ASCII.GetString(encrypted));

        var decryptor = new GraalEncryption(EncryptionGeneration.Gen3);
        decryptor.Reset(0);
        Assert.Equal(")abd", Encoding.ASCII.GetString(decryptor.Decrypt(encrypted)));
    }

    [Fact]
    public void Gen5XorEncryptsUsingLittleEndianIteratorBytesAndCompressionLimit()
    {
        var codec = new GraalEncryption(EncryptionGeneration.Gen5);
        codec.Reset(0);
        codec.LimitFromCompressionType(CompressionType.Uncompressed);

        var encrypted = codec.Encrypt(new byte[] { 1, 2, 3, 4, 5 });

        Assert.Equal(new byte[] { 0x19, 0x1A, 0xD2, 0xD2, 0x7D }, encrypted);
    }
}

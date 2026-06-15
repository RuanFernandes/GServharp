using System.Text;
using GServ.Protocol;

namespace GServ.Protocol.Tests;

public sealed class GraalEncryptionTests
{
    [Fact]
    public void DefaultsToGeneration3IteratorStartFromGs2lib()
    {
        Assert.Equal(0u, GraalEncryption.IteratorStart[0]);
        Assert.Equal(0u, GraalEncryption.IteratorStart[1]);
        Assert.Equal(0x04A80B38u, GraalEncryption.IteratorStart[2]);
        Assert.Equal(0x04A80B38u, GraalEncryption.IteratorStart[3]);
        Assert.Equal(0x04A80B38u, GraalEncryption.IteratorStart[4]);
        Assert.Equal(0u, GraalEncryption.IteratorStart[5]);
    }

    [Fact]
    public void Generation3EncryptInsertsParenAtIteratorPosition()
    {
        var encryption = new GraalEncryption();
        encryption.Generation = GraalEncryptionGeneration.Gen3;
        encryption.Reset(0);

        var encrypted = encryption.Encrypt("ABC"u8);

        Assert.Equal(")ABC", Encoding.Latin1.GetString(encrypted));
    }

    [Fact]
    public void Generation3DecryptRemovesByteAtIteratorPosition()
    {
        var encryption = new GraalEncryption();
        encryption.Generation = GraalEncryptionGeneration.Gen3;
        encryption.Reset(0);

        var decrypted = encryption.Decrypt(")ABC"u8);

        Assert.Equal("ABC", Encoding.Latin1.GetString(decrypted));
    }

    [Fact]
    public void Generation4And5XorUsingLittleEndianIteratorBytes()
    {
        var encryption = new GraalEncryption();
        encryption.Generation = GraalEncryptionGeneration.Gen4;
        encryption.Reset(7);

        var encrypted = encryption.Encrypt("ABCD"u8);

        Assert.Equal([94, 90, 146, 146], encrypted);
    }

    [Fact]
    public void Generation5LimitFromCompressionTypeStopsAfterConfiguredBlocks()
    {
        var encryption = new GraalEncryption();
        encryption.Generation = GraalEncryptionGeneration.Gen5;
        encryption.Reset(7);

        Assert.True(encryption.LimitFromType(GraalCompressionType.Zlib));

        var encrypted = encryption.Encrypt(new byte[20]);

        Assert.NotEqual(0, encrypted[15]);
        Assert.Equal(0, encrypted[16]);
        Assert.Equal(0, encrypted[19]);
    }
}

using GServ.Protocol;

namespace GServ.Protocol.Tests;

public sealed class ProtocolSourceStatusTests
{
    [Fact]
    public void NumericPacketIdsAreRecoveredFromGs2libIEnumsHeader()
    {
        Assert.Equal("IEnums.h", PacketIdSourceStatus.AuthoritativeEnumHeader);
        Assert.Equal("https://xtjoeytx@bitbucket.org/xtjoeytx/gs2lib.git", PacketIdSourceStatus.AuthoritativeRepositoryUrl);
        Assert.Equal("63b1ae96491c188905b50c6b61c8532c601a2122", PacketIdSourceStatus.AuthoritativeCommit);
        Assert.True(PacketIdSourceStatus.NumericPacketIdsRecovered);
    }

    [Fact]
    public void WireConstantsMatchCppPacketHandlerShape()
    {
        Assert.Equal(2, ProtocolWireConstants.PacketBundleLengthPrefixBytes);
        Assert.Equal(10, ProtocolWireConstants.PacketTerminator);
        Assert.Equal(256, ProtocolWireConstants.HandlerTableSize);
    }

    [Fact]
    public void ProtocolCriticalCppDependencyHeadersAreRecoveredFromGs2lib()
    {
        Assert.Equal("gs2lib", ProtocolDependencySourceStatus.ExpectedSourceDependency);
        Assert.Equal("gs2lib_SOURCE_DIR/include", ProtocolDependencySourceStatus.ExpectedSourceIncludePath);
        Assert.Equal("https://xtjoeytx@bitbucket.org/xtjoeytx/gs2lib.git", ProtocolDependencySourceStatus.RecoveredRepositoryUrl);
        Assert.Equal("63b1ae96491c188905b50c6b61c8532c601a2122", ProtocolDependencySourceStatus.RecoveredCommit);
        Assert.True(ProtocolDependencySourceStatus.IEnumsHeaderRecovered);
        Assert.True(ProtocolDependencySourceStatus.CStringHeaderRecovered);
        Assert.True(ProtocolDependencySourceStatus.CEncryptionHeaderRecovered);
        Assert.True(ProtocolDependencySourceStatus.CFileQueueHeaderRecovered);
        Assert.True(ProtocolDependencySourceStatus.CSocketHeaderRecovered);
    }

    [Fact]
    public void ProtocolCriticalPacketIdsMatchRecoveredIEnumsHeader()
    {
        Assert.Equal(50, (byte)ClientPacketId.PLI_RAWDATA);
        Assert.Equal(252, (byte)ClientPacketId.PLI_SET_ENC_KEY);
        Assert.Equal(253, (byte)ClientPacketId.PLI_BUNDLE);
        Assert.Equal(16, (byte)ServerPacketId.PLO_DISCMESSAGE);
        Assert.Equal(100, (byte)ServerPacketId.PLO_RAWDATA);
        Assert.Equal(101, (byte)ServerPacketId.PLO_BOARDPACKET);
        Assert.Equal(102, (byte)ServerPacketId.PLO_FILE);
        Assert.Equal(252, (byte)ServerPacketId.PLO_SET_ENC_KEY);
        Assert.Equal(253, (byte)ServerPacketId.PLO_BUNDLE);
    }
}

using System.Text;
using GServ.Protocol;
using Xunit;

namespace GServ.Protocol.Tests;

public sealed class LoginPacketParserTests
{
    [Fact]
    public void Client3LoginReadsKeyVersionAccountPasswordAndIdentity()
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar(5);
        packet.WriteGChar(42);
        packet.WriteBytes(Encoding.ASCII.GetBytes("G3D0311C"));
        packet.WriteGChar(4);
        packet.WriteBytes(Encoding.ASCII.GetBytes("Ruan"));
        packet.WriteGChar(2);
        packet.WriteBytes(Encoding.ASCII.GetBytes("pw"));
        packet.WriteBytes(Encoding.ASCII.GetBytes("win,\"\",disk,net,\"6.2 9200 \""));

        var login = LoginPacketParser.Parse(packet.ToArray());

        Assert.Equal(PlayerSessionType.Client3, login.Type);
        Assert.Equal(EncryptionGeneration.Gen5, login.InboundGeneration);
        Assert.Equal((byte)42, login.EncryptionKey);
        Assert.Equal("G3D0311C", login.VersionToken);
        Assert.Equal(ClientVersionId.Client6037, login.VersionId);
        Assert.Equal("Ruan", login.AccountName);
        Assert.Equal("pw", login.Password);
        Assert.Equal("win,\"\",disk,net,\"6.2 9200 \"", login.Identity);
        Assert.Equal("win", login.Platform);
    }

    [Fact]
    public void LegacyClientWithKnownVersionDoesNotReadEncryptionKey()
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar(0);
        packet.WriteBytes(Encoding.ASCII.GetBytes("GNW13110"));
        packet.WriteGChar(3);
        packet.WriteBytes(Encoding.ASCII.GetBytes("acc"));
        packet.WriteGChar(4);
        packet.WriteBytes(Encoding.ASCII.GetBytes("pass"));

        var login = LoginPacketParser.Parse(packet.ToArray());

        Assert.Equal(PlayerSessionType.Client, login.Type);
        Assert.Equal(EncryptionGeneration.Gen2, login.InboundGeneration);
        Assert.Null(login.EncryptionKey);
        Assert.Equal("GNW13110", login.VersionToken);
        Assert.Equal(ClientVersionId.Client1411, login.VersionId);
        Assert.Equal("acc", login.AccountName);
        Assert.Equal("pass", login.Password);
    }

    [Fact]
    public void LegacyClientWithUnknownInitialVersionRewindsAndReadsKeyThenVersion()
    {
        var packet = new GraalBinaryWriter();
        packet.WriteGChar(0);
        packet.WriteGChar(7);
        packet.WriteBytes(Encoding.ASCII.GetBytes("GNW13110"));
        packet.WriteGChar(5);
        packet.WriteBytes(Encoding.ASCII.GetBytes("guest"));
        packet.WriteGChar(0);

        var login = LoginPacketParser.Parse(packet.ToArray());

        Assert.Equal(PlayerSessionType.Client, login.Type);
        Assert.Equal(EncryptionGeneration.Gen3, login.InboundGeneration);
        Assert.Equal((byte)7, login.EncryptionKey);
        Assert.Equal("GNW13110", login.VersionToken);
        Assert.Equal(ClientVersionId.Client1411, login.VersionId);
        Assert.Equal("guest", login.AccountName);
        Assert.Equal(string.Empty, login.Password);
    }
}

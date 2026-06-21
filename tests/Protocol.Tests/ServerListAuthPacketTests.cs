using System.Text;
using Preagonal.GServer.Protocol;
using Xunit;

namespace Preagonal.GServer.Protocol.Tests;

public sealed class ServerListAuthPacketTests
{
    [Fact]
    public void VerifyAccount2RequestMatchesCppFieldOrder()
    {
        var bytes = ServerListAuthPackets.VerifyAccount2Request(
            accountName: "Ruan",
            password: "pw",
            playerId: 7,
            type: PlayerSessionType.Client3,
            identity: "win");

        Assert.Equal(
            new byte[]
            {
                49,
                36, 82, 117, 97, 110,
                34, 112, 119,
                32, 39,
                64,
                32, 35, 119, 105, 110
            },
            bytes);
    }

    [Fact]
    public void VerifyAccount2ResponseParsesAccountIdTypeAndMessage()
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)ListServerToServerPacketId.VerifyAccount2);
        writer.WriteGChar(4);
        writer.WriteBytes(Encoding.ASCII.GetBytes("Ruan"));
        writer.WriteGShort(7);
        writer.WriteGChar((byte)PlayerSessionType.Client3);
        writer.WriteBytes(Encoding.ASCII.GetBytes("SUCCESS"));

        var response = ServerListAuthPackets.ParseVerifyAccount2Response(writer.ToArray()[1..]);

        Assert.Equal("Ruan", response.AccountName);
        Assert.Equal(7, response.PlayerId);
        Assert.Equal(PlayerSessionType.Client3, response.Type);
        Assert.Equal("SUCCESS", response.Message);
        Assert.True(response.IsSuccess);
    }

    [Fact]
    public void RegisterV3PacketUsesConfirmedOpcodeAndRawVersionText()
    {
        var bytes = ServerListAuthPackets.RegisterV3("3.0.9-beta");

        Assert.Equal([(byte)62, (byte)'3', (byte)'.', (byte)'0', (byte)'.', (byte)'9', (byte)'-', (byte)'b', (byte)'e', (byte)'t', (byte)'a'], bytes);
    }

    [Fact]
    public void NewServerPacketMatchesCppFieldOrder()
    {
        var bytes = ServerListAuthPackets.NewServer(
            name: "Classic",
            description: "Desc",
            language: "English",
            version: "3.0.9-beta",
            url: "http://example.test/",
            ip: "AUTO",
            port: "14900",
            localIp: "10.0.0.5");

        Assert.Equal(
            new byte[]
            {
                54,
                39, 67, 108, 97, 115, 115, 105, 99,
                36, 68, 101, 115, 99,
                39, 69, 110, 103, 108, 105, 115, 104,
                42, 51, 46, 48, 46, 57, 45, 98, 101, 116, 97,
                52, 104, 116, 116, 112, 58, 47, 47, 101, 120, 97, 109, 112, 108, 101, 46, 116, 101, 115, 116, 47,
                36, 65, 85, 84, 79,
                37, 49, 52, 57, 48, 48,
                40, 49, 48, 46, 48, 46, 48, 46, 53
            },
            bytes);
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 3)]
    public void ServerHqLevelUsesZeroForStaffOnlyOtherwiseConfiguredLevel(bool onlyStaff, int expectedLevel)
    {
        var bytes = ServerListAuthPackets.ServerHqLevel(onlyStaff, configuredLevel: 3);

        Assert.Equal([(byte)56, (byte)(expectedLevel + 32)], bytes);
    }

    [Fact]
    public void VersionConfigSendTextUsesGTokenizedAllowedVersions()
    {
        var bytes = ServerListAuthPackets.AllowedVersionsText(["G3D0311C", "name,with/slash"]);

        Assert.Equal(
            "?" + "Listserver,settings,allowedversions,G3D0311C,\"name,with/slash\"",
            Encoding.ASCII.GetString(bytes));
    }

    [Fact]
    public void PingPacketUsesConfirmedServerToListOpcode()
    {
        Assert.Equal([(byte)48], ServerListAuthPackets.Ping());
    }

    [Fact]
    public void RequestListTextForPlayerUsesConfirmedOpcodeAndGShortPlayerId()
    {
        var bytes = ServerListAuthPackets.RequestListTextForPlayer(7, "accounts");

        Assert.Equal(
            new byte[] { 58, 32, 39, 97, 99, 99, 111, 117, 110, 116, 115 },
            bytes);
    }

    [Fact]
    public void ServerInfoUsesCppOpcode()
    {
        var bytes = ServerListAuthPackets.ServerInfoForPlayer(7, "Login");

        Assert.Equal(
            new byte[] { 50, 32, 39, 76, 111, 103, 105, 110 },
            bytes);
    }
}

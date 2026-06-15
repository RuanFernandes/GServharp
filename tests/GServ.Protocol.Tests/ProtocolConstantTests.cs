using GServ.Protocol;
using Xunit;

namespace GServ.Protocol.Tests;

public sealed class ProtocolConstantTests
{
    [Fact]
    public void ConfirmedCorePacketIdsMatchGs2libIEnums()
    {
        Assert.Equal(50, (int)PlayerToServerPacketId.RawData);
        Assert.Equal(252, (int)PlayerToServerPacketId.SetEncryptionKey);
        Assert.Equal(253, (int)PlayerToServerPacketId.Bundle);
        Assert.Equal(0, (int)ServerToPlayerPacketId.LevelBoard);
        Assert.Equal(2, (int)ServerToPlayerPacketId.BaddyProps);
        Assert.Equal(4, (int)ServerToPlayerPacketId.LevelChest);
        Assert.Equal(6, (int)ServerToPlayerPacketId.LevelName);
        Assert.Equal(7, (int)ServerToPlayerPacketId.BoardModify);
        Assert.Equal(8, (int)ServerToPlayerPacketId.OtherPlayerProps);
        Assert.Equal(9, (int)ServerToPlayerPacketId.PlayerProps);
        Assert.Equal(10, (int)ServerToPlayerPacketId.IsLeader);
        Assert.Equal(17, (int)ServerToPlayerPacketId.HorseAdd);
        Assert.Equal(16, (int)ServerToPlayerPacketId.DisconnectMessage);
        Assert.Equal(25, (int)ServerToPlayerPacketId.Signature);
        Assert.Equal(28, (int)ServerToPlayerPacketId.FlagSet);
        Assert.Equal(34, (int)ServerToPlayerPacketId.NpcWeaponDelete);
        Assert.Equal(100, (int)ServerToPlayerPacketId.RawData);
        Assert.Equal(101, (int)ServerToPlayerPacketId.BoardPacket);
        Assert.Equal(102, (int)ServerToPlayerPacketId.File);
        Assert.Equal(107, (int)ServerToPlayerPacketId.BoardLayer);
        Assert.Equal(156, (int)ServerToPlayerPacketId.SetActiveLevel);
        Assert.Equal(174, (int)ServerToPlayerPacketId.GhostIcon);
        Assert.Equal(190, (int)ServerToPlayerPacketId.ServerListConnected);
        Assert.Equal(194, (int)ServerToPlayerPacketId.ClearWeapons);
        Assert.Equal(252, (int)ServerToPlayerPacketId.SetEncryptionKey);
        Assert.Equal(253, (int)ServerToPlayerPacketId.Bundle);
        Assert.Equal(14, (int)ServerToListServerPacketId.PlayerAdd);
    }

    [Fact]
    public void PlayerTypeBitsMatchGs2libIEnums()
    {
        Assert.Equal(1, (int)PlayerSessionType.Client);
        Assert.Equal(2, (int)PlayerSessionType.RemoteControl);
        Assert.Equal(4, (int)PlayerSessionType.NpcServer);
        Assert.Equal(8, (int)PlayerSessionType.NpcControl);
        Assert.Equal(16, (int)PlayerSessionType.Client2);
        Assert.Equal(32, (int)PlayerSessionType.Client3);
        Assert.Equal(64, (int)PlayerSessionType.RemoteControl2);
        Assert.Equal(256, (int)PlayerSessionType.Web);
    }

    [Fact]
    public void PlayerPropertyIdsMatchAccountHeader()
    {
        Assert.Equal(0, (int)PlayerPropertyId.Nickname);
        Assert.Equal(1, (int)PlayerPropertyId.MaxPower);
        Assert.Equal(2, (int)PlayerPropertyId.CurrentPower);
        Assert.Equal(3, (int)PlayerPropertyId.RupeesCount);
        Assert.Equal(8, (int)PlayerPropertyId.SwordPower);
        Assert.Equal(20, (int)PlayerPropertyId.CurrentLevel);
        Assert.Equal(30, (int)PlayerPropertyId.IpAddress);
        Assert.Equal(34, (int)PlayerPropertyId.AccountName);
        Assert.Equal(37, (int)PlayerPropertyId.GAttrib1);
        Assert.Equal(74, (int)PlayerPropertyId.GAttrib30);
        Assert.Equal(82, (int)PlayerPropertyId.CommunityName);
    }
}

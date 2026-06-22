using Preagonal.GServer.Protocol;
using Xunit;

namespace Preagonal.GServer.Protocol.Tests;

public sealed class ProtocolConstantTests
{
    [Fact]
    public void ConfirmedCorePacketIdsMatchGs2libIEnums()
    {
        Assert.Equal(50, (int)PlayerToServerPacketId.RawData);
        Assert.Equal(41, (int)PlayerToServerPacketId.ServerWarp);
        Assert.Equal(12, (int)PlayerToServerPacketId.ItemAdd);
        Assert.Equal(13, (int)PlayerToServerPacketId.ItemDelete);
        Assert.Equal(14, (int)PlayerToServerPacketId.ClaimPker);
        Assert.Equal(20, (int)PlayerToServerPacketId.OpenChest);
        Assert.Equal(24, (int)PlayerToServerPacketId.ShowImg);
        Assert.Equal(28, (int)PlayerToServerPacketId.PrivateMessage);
        Assert.Equal(29, (int)PlayerToServerPacketId.NpcWeaponDelete);
        Assert.Equal(32, (int)PlayerToServerPacketId.ItemTake);
        Assert.Equal(33, (int)PlayerToServerPacketId.WeaponAdd);
        Assert.Equal(38, (int)PlayerToServerPacketId.TriggerAction);
        Assert.Equal(252, (int)PlayerToServerPacketId.SetEncryptionKey);
        Assert.Equal(253, (int)PlayerToServerPacketId.Bundle);
        Assert.Equal(0, (int)ServerToPlayerPacketId.LevelBoard);
        Assert.Equal(1, (int)ServerToPlayerPacketId.LevelLink);
        Assert.Equal(2, (int)ServerToPlayerPacketId.BaddyProps);
        Assert.Equal(4, (int)ServerToPlayerPacketId.LevelChest);
        Assert.Equal(5, (int)ServerToPlayerPacketId.LevelSign);
        Assert.Equal(6, (int)ServerToPlayerPacketId.LevelName);
        Assert.Equal(7, (int)ServerToPlayerPacketId.BoardModify);
        Assert.Equal(8, (int)ServerToPlayerPacketId.OtherPlayerProps);
        Assert.Equal(9, (int)ServerToPlayerPacketId.PlayerProps);
        Assert.Equal(10, (int)ServerToPlayerPacketId.IsLeader);
        Assert.Equal(17, (int)ServerToPlayerPacketId.HorseAdd);
        Assert.Equal(16, (int)ServerToPlayerPacketId.DisconnectMessage);
        Assert.Equal(25, (int)ServerToPlayerPacketId.Signature);
        Assert.Equal(28, (int)ServerToPlayerPacketId.FlagSet);
        Assert.Equal(30, (int)ServerToPlayerPacketId.FileSendFailed);
        Assert.Equal(32, (int)ServerToPlayerPacketId.ShowImg);
        Assert.Equal(34, (int)ServerToPlayerPacketId.NpcWeaponDelete);
        Assert.Equal(37, (int)ServerToPlayerPacketId.PrivateMessage);
        Assert.Equal(45, (int)ServerToPlayerPacketId.FileUpToDate);
        Assert.Equal(48, (int)ServerToPlayerPacketId.TriggerAction);
        Assert.Equal(100, (int)ServerToPlayerPacketId.RawData);
        Assert.Equal(101, (int)ServerToPlayerPacketId.BoardPacket);
        Assert.Equal(102, (int)ServerToPlayerPacketId.File);
        Assert.Equal(105, (int)ServerToPlayerPacketId.UpdatePackageSize);
        Assert.Equal(106, (int)ServerToPlayerPacketId.UpdatePackageDone);
        Assert.Equal(107, (int)ServerToPlayerPacketId.BoardLayer);
        Assert.Equal(153, (int)ServerToPlayerPacketId.Say2);
        Assert.Equal(156, (int)ServerToPlayerPacketId.SetActiveLevel);
        Assert.Equal(187, (int)ServerToPlayerPacketId.UpdatePackageIsUpdated);
        Assert.Equal(174, (int)ServerToPlayerPacketId.GhostIcon);
        Assert.Equal(178, (int)ServerToPlayerPacketId.ServerWarp);
        Assert.Equal(190, (int)ServerToPlayerPacketId.ServerListConnected);
        Assert.Equal(194, (int)ServerToPlayerPacketId.ClearWeapons);
        Assert.Equal(252, (int)ServerToPlayerPacketId.SetEncryptionKey);
        Assert.Equal(253, (int)ServerToPlayerPacketId.Bundle);
        Assert.Equal(5, (int)ServerToListServerPacketId.SetIp);
        Assert.Equal(7, (int)ServerToListServerPacketId.PlayerSet);
        Assert.Equal(14, (int)ServerToListServerPacketId.PlayerAdd);
        Assert.Equal(15, (int)ServerToListServerPacketId.PlayerRemove);
        Assert.Equal(18, (int)ServerToListServerPacketId.ServerInfo);
        Assert.Equal(17, (int)ServerToListServerPacketId.VerifyAccount2);
        Assert.Equal(22, (int)ServerToListServerPacketId.NewServer);
        Assert.Equal(23, (int)ServerToListServerPacketId.ServerHqPass);
        Assert.Equal(24, (int)ServerToListServerPacketId.ServerHqLevel);
        Assert.Equal(30, (int)ServerToListServerPacketId.RegisterV3);
        Assert.Equal(31, (int)ServerToListServerPacketId.SendText);
        Assert.Equal(99, (int)ListServerToServerPacketId.Ping);
    }

    [Fact]
    public void ConfirmedRcNcPacketIdsMatchGs2libIEnums()
    {
        Assert.Equal(51, (int)PlayerToServerPacketId.RcServerOptionsGet);
        Assert.Equal(52, (int)PlayerToServerPacketId.RcServerOptionsSet);
        Assert.Equal(53, (int)PlayerToServerPacketId.RcFolderConfigGet);
        Assert.Equal(54, (int)PlayerToServerPacketId.RcFolderConfigSet);
        Assert.Equal(55, (int)PlayerToServerPacketId.RcRespawnSet);
        Assert.Equal(56, (int)PlayerToServerPacketId.RcHorseLifeSet);
        Assert.Equal(57, (int)PlayerToServerPacketId.RcApIncrementSet);
        Assert.Equal(58, (int)PlayerToServerPacketId.RcBaddyRespawnSet);
        Assert.Equal(67, (int)PlayerToServerPacketId.RcApplyReason);
        Assert.Equal(73, (int)PlayerToServerPacketId.RcPlayerPropsGetById);
        Assert.Equal(74, (int)PlayerToServerPacketId.RcPlayerPropsGetByAccount);
        Assert.Equal(75, (int)PlayerToServerPacketId.RcPlayerPropsReset);
        Assert.Equal(76, (int)PlayerToServerPacketId.RcPlayerPropsSetById);
        Assert.Equal(83, (int)PlayerToServerPacketId.RcPlayerRightsGet);
        Assert.Equal(84, (int)PlayerToServerPacketId.RcPlayerRightsSet);
        Assert.Equal(89, (int)PlayerToServerPacketId.RcFileBrowserStart);
        Assert.Equal(92, (int)PlayerToServerPacketId.RcFileBrowserDownload);
        Assert.Equal(103, (int)PlayerToServerPacketId.NcNpcGet);
        Assert.Equal(106, (int)PlayerToServerPacketId.NcNpcScriptGet);
        Assert.Equal(115, (int)PlayerToServerPacketId.NcWeaponListGet);
        Assert.Equal(116, (int)PlayerToServerPacketId.NcWeaponGet);
        Assert.Equal(150, (int)PlayerToServerPacketId.NcLevelListGet);

        Assert.Equal(62, (int)ServerToPlayerPacketId.RcPlayerRightsGet);
        Assert.Equal(65, (int)ServerToPlayerPacketId.RcFileBrowserDirList);
        Assert.Equal(66, (int)ServerToPlayerPacketId.RcFileBrowserDir);
        Assert.Equal(67, (int)ServerToPlayerPacketId.RcFileBrowserMessage);
        Assert.Equal(74, (int)ServerToPlayerPacketId.RcChat);
        Assert.Equal(79, (int)ServerToPlayerPacketId.NpcServerAddress);
        Assert.Equal(103, (int)ServerToPlayerPacketId.RcMaxUploadFileSize);
        Assert.Equal(180, (int)ServerToPlayerPacketId.StatusList);
        Assert.Equal(157, (int)ServerToPlayerPacketId.NcNpcAttributes);
        Assert.Equal(160, (int)ServerToPlayerPacketId.NcNpcScript);
        Assert.Equal(167, (int)ServerToPlayerPacketId.NcWeaponListGet);
        Assert.Equal(192, (int)ServerToPlayerPacketId.NcWeaponGet);

        Assert.Equal(16, (int)ServerToListServerPacketId.Ping);
        Assert.Equal(26, (int)ServerToListServerPacketId.RequestList);
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

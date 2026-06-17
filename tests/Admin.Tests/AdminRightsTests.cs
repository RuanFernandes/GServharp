using GServ.Admin;
using Xunit;

namespace GServ.Admin.Tests;

public sealed class AdminRightsTests
{
    [Fact]
    public void ConfirmedRightBitsMatchAccountHeader()
    {
        Assert.Equal(0x00001, (int)AdminRight.WarpTo);
        Assert.Equal(0x00010, (int)AdminRight.Disconnect);
        Assert.Equal(0x00400, (int)AdminRight.SetRights);
        Assert.Equal(0x04000, (int)AdminRight.ModifyStaffAccount);
        Assert.Equal(0x80000, (int)AdminRight.NpcControl);
        Assert.Equal(0xFFFFFF, (int)AdminRight.AnyRight);
    }

    [Fact]
    public void HasRightMatchesCppBitwiseCheck()
    {
        var rights = AdminRight.SetRights | AdminRight.ModifyStaffAccount;

        Assert.True(AdminRights.HasRight(rights, AdminRight.SetRights));
        Assert.False(AdminRights.HasRight(rights, AdminRight.NpcControl));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void ControlLoginRejectsMissingStaffOrAdminIp(bool isStaff, bool isAdminIp)
    {
        var decision = ControlLoginGate.Evaluate(isStaff, isAdminIp);

        Assert.False(decision.Allowed);
        Assert.Equal("You do not have RC rights.", decision.DisconnectMessage);
    }

    [Fact]
    public void ControlLoginAllowsStaffFromAdminIp()
    {
        var decision = ControlLoginGate.Evaluate(isStaff: true, isAdminIp: true);

        Assert.True(decision.Allowed);
        Assert.Null(decision.DisconnectMessage);
    }

    [Fact]
    public void FolderRightLineNormalizesRightsFolderAndWildcardLikePlayerRc()
    {
        var entry = RcFolderRights.ParseLine(@"RW levels\*.nw");

        Assert.Equal("rw", entry.Rights);
        Assert.Equal("levels/", entry.Folder);
        Assert.Equal("*.nw", entry.Wildcard);
    }

    [Fact]
    public void EmptyFolderRightLineUsesCppDefaults()
    {
        var entry = RcFolderRights.ParseLine("");

        Assert.Equal("r", entry.Rights);
        Assert.Equal("*", entry.Folder);
        Assert.Equal("*", entry.Wildcard);
    }

    [Fact]
    public void ProtectedFileDownloadRequiresModifyStaffAccountRight()
    {
        var blocked = RcProtectedFiles.EvaluateDownload("config/adminconfig.txt", AdminRight.SetRights);
        var allowed = RcProtectedFiles.EvaluateDownload("config/adminconfig.txt", AdminRight.ModifyStaffAccount);

        Assert.False(blocked.Allowed);
        Assert.Equal("Insufficient rights to download/view config/adminconfig.txt", blocked.Message);
        Assert.True(allowed.Allowed);
        Assert.Null(allowed.Message);
    }
}

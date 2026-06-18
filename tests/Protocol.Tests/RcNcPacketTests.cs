using System.Text;
using Preagonal.GServer.Protocol;
using Xunit;

namespace Preagonal.GServer.Protocol.Tests;

public sealed class RcNcPacketTests
{
    [Fact]
    public void RcChatPacketUsesConfirmedOpcodeAndTrailingNewline()
    {
        var bytes = RcNcPackets.RcChat("New RC: Ruan");

        Assert.Equal(
            new byte[] { 106, 78, 101, 119, 32, 82, 67, 58, 32, 82, 117, 97, 110, 10 },
            bytes);
    }

    [Fact]
    public void RcMaxUploadFileSizeUsesConfirmedTwentyMebibyteGInt5()
    {
        var bytes = RcNcPackets.RcMaxUploadFileSize(20 * 1024 * 1024);

        Assert.Equal(new byte[] { 135, 32, 42, 32, 32, 32, 10 }, bytes);
    }

    [Fact]
    public void RcLoginPacketsUseCppOpcodesAndTextShapes()
    {
        Assert.Equal(new byte[] { 226, 10 }, RcNcPackets.ClearWeapons());
        Assert.Equal(new byte[] { 222, 10 }, RcNcPackets.Unknown190());
        Assert.Equal(
            Encoding.ASCII.GetBytes("O\"Server\",\"Manager\"\n"),
            RcNcPackets.StaffGuilds("Server, Manager"));
        Assert.Equal(
            new byte[] { 212 }
                .Concat(Encoding.ASCII.GetBytes("Online,Away,No PMs\n"))
                .ToArray(),
            RcNcPackets.StatusList("Online, Away, No PMs"));
    }

    [Fact]
    public void FileBrowserMessageUsesConfirmedOpcodeAndTextPayload()
    {
        var bytes = RcNcPackets.FileBrowserMessage("Welcome to the File Browser.");

        Assert.Equal(
            Encoding.ASCII.GetBytes("cWelcome to the File Browser.\n"),
            bytes);
    }

    [Fact]
    public void RcControlPacketsUseConfirmedOpcodes()
    {
        Assert.Equal(
            Encoding.ASCII.GetBytes("CAdmin Moon:\u00a7hello\n"),
            RcNcPackets.RcAdminMessage("Admin Moon:\u00a7hello"));
        Assert.Equal(
            Encoding.ASCII.GetBytes("l\"name = GSharp\",\"serverport = 14899\"\n"),
            RcNcPackets.ServerOptionsGet("name = GSharp\nserverport = 14899\n"));
        Assert.Equal(
            Encoding.ASCII.GetBytes("m\"level *.nw\",\"level levels/*.graal\"\n"),
            RcNcPackets.FolderConfigGet("level *.nw\nlevel levels/*.graal\n"));
        Assert.Equal(
            new byte[] { 93, 32, 33, 41, 116, 101, 115, 116, 61, 116, 114, 117, 101, 10 },
            RcNcPackets.ServerFlagsGet([new KeyValuePair<string, string>("test", "true")]));
    }

    [Fact]
    public void RcAddPlayerUsesCppPlayerListShape()
    {
        var bytes = RcNcPackets.AddPlayer(
            7,
            "pc:Ruan",
            "start.nw",
            statusMessage: 0,
            nickname: "Ruan",
            communityName: "Ruan");

        Assert.Equal(
            new byte[]
            {
                87, 32, 39,
                39, 112, 99, 58, 82, 117, 97, 110,
                52, 40, 115, 116, 97, 114, 116, 46, 110, 119,
                85, 32,
                32, 36, 82, 117, 97, 110,
                114, 36, 82, 117, 97, 110,
                10
            },
            bytes);
    }

    [Fact]
    public void FileBrowserDirPacketMatchesCppFieldOrder()
    {
        var bytes = RcNcPackets.FileBrowserDir(
            "levels/",
            [new RcFileBrowserEntry("start.nw", "rw", Size: 100, ModifiedTime: 1)]);

        Assert.Equal(
            new byte[]
            {
                98,
                39, 108, 101, 118, 101, 108, 115, 47,
                32,
                54,
                40, 115, 116, 97, 114, 116, 46, 110, 119,
                34, 114, 119,
                32, 32, 32, 32, 132,
                32, 32, 32, 32, 33,
                10
            },
            bytes);
    }

    [Fact]
    public void NcWeaponListUsesConfirmedOpcodeAndGCharNameLengths()
    {
        var bytes = RcNcPackets.NcWeaponList(["SwordTool", "Gui"]);

        Assert.Equal(new byte[] { 199, 41, 83, 119, 111, 114, 100, 84, 111, 111, 108, 35, 71, 117, 105, 10 }, bytes);
    }

    [Fact]
    public void NcWeaponGetReplacesScriptNewlinesWithSectionByte()
    {
        var bytes = RcNcPackets.NcWeaponGet("Tool", "tool.png", "a\nb");

        Assert.Equal(
            new byte[] { 224, 36, 84, 111, 111, 108, 40, 116, 111, 111, 108, 46, 112, 110, 103, 97, 167, 98, 10 },
            bytes);
    }

    [Fact]
    public void LegacyNcWeaponGetUsesNpcWeaponAddShapeWithSectionByteNewlines()
    {
        var bytes = RcNcPackets.LegacyNcWeaponGet("Tool", "tool.png", "a\nb");

        Assert.Equal(
            new byte[] { 65, 36, 84, 111, 111, 108, 32, 40, 116, 111, 111, 108, 46, 112, 110, 103, 33, 32, 35, 97, 167, 98, 10 },
            bytes);
    }

    [Fact]
    public void NcClassGetUsesConfirmedOpcodeNameAndGTokenizedSource()
    {
        var bytes = RcNcPackets.NcClassGet("foo", "a\nb,c");

        Assert.Equal(
            new byte[] { 194, 35, 102, 111, 111, 97, 44, 34, 98, 44, 99, 34, 10 },
            bytes);
    }

    [Fact]
    public void NcClassAddAndDeleteBroadcastRawClassNamePayloads()
    {
        Assert.Equal(
            new byte[] { 195, 102, 111, 111, 10 },
            RcNcPackets.NcClassAdd("foo"));

        Assert.Equal(
            new byte[] { 220, 102, 111, 111, 10 },
            RcNcPackets.NcClassDelete("foo"));
    }

    [Fact]
    public void NpcServerAddressUsesConfirmedOpcodeGShortIdAndCommaAddress()
    {
        var bytes = RcNcPackets.NpcServerAddress(7, "127.0.0.1", 14950);

        Assert.Equal(
            Encoding.ASCII.GetBytes("o '127.0.0.1,14950\n"),
            bytes);
    }
}

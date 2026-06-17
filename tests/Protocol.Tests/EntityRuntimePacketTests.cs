using System.Text;
using GServ.Protocol;
using Xunit;

namespace GServ.Protocol.Tests;

public sealed class EntityRuntimePacketTests
{
    [Fact]
    public void ConfirmedEntityPacketIdsMatchGs2libIEnums()
    {
        Assert.Equal(2, (int)ServerToPlayerPacketId.BaddyProps);
        Assert.Equal(3, (int)ServerToPlayerPacketId.NpcProps);
        Assert.Equal(17, (int)ServerToPlayerPacketId.HorseAdd);
        Assert.Equal(18, (int)ServerToPlayerPacketId.HorseDelete);
        Assert.Equal(22, (int)ServerToPlayerPacketId.ItemAdd);
        Assert.Equal(23, (int)ServerToPlayerPacketId.ItemDelete);
        Assert.Equal(29, (int)ServerToPlayerPacketId.NpcDelete);
        Assert.Equal(33, (int)ServerToPlayerPacketId.NpcWeaponAdd);
        Assert.Equal(43, (int)ServerToPlayerPacketId.DefaultWeapon);
        Assert.Equal(131, (int)ServerToPlayerPacketId.NpcBytecode);
        Assert.Equal(134, (int)ServerToPlayerPacketId.GaniScript);
        Assert.Equal(195, (int)ServerToPlayerPacketId.LoadGani);
        Assert.Equal(150, (int)ServerToPlayerPacketId.NpcDelete2);
    }

    [Fact]
    public void UpdateGaniPacketIdMatchesGs2libIEnums()
    {
        Assert.Equal(157, (int)PlayerToServerPacketId.UpdateGani);
    }

    [Fact]
    public void ItemAddAndDeleteUseConfirmedOpcodesAndEncodedCoordinatePayload()
    {
        Assert.Equal(new byte[] { 54, 53, 55, 34, 10 }, EntityPackets.ItemAdd(encodedX: 21, encodedY: 23, itemType: 2));
        Assert.Equal(new byte[] { 55, 53, 55, 10 }, EntityPackets.ItemDelete(encodedX: 21, encodedY: 23));
    }

    [Fact]
    public void ItemDeleteFromLevelCoordinatesMultipliesByTwoLikeLevelCleanup()
    {
        Assert.Equal(new byte[] { 55, 53, 55, 10 }, EntityPackets.ItemDeleteFromLevelCoordinates(10.5f, 11.5f));
    }

    [Fact]
    public void HorseAddMatchesLevelHorsePacketShape()
    {
        var bytes = EntityPackets.HorseAdd(x: 30.5f, y: 31.0f, direction: 2, bushes: 1, image: "horse.png");

        Assert.Equal(
            new byte[] { 49, 61, 94, 38, 104, 111, 114, 115, 101, 46, 112, 110, 103, 10 },
            bytes);
    }

    [Fact]
    public void HorseDeleteMultipliesCoordinatesByTwo()
    {
        Assert.Equal(new byte[] { 50, 93, 94, 10 }, EntityPackets.HorseDelete(30.5f, 31.0f));
    }

    [Fact]
    public void DefaultWeaponPacketUsesConfirmedOpcodeAndItemId()
    {
        Assert.Equal(new byte[] { 75, 39, 10 }, EntityPackets.DefaultWeapon(7));
    }

    [Fact]
    public void NpcWeaponAddWithoutScriptWritesNameImagePropertyAndNewline()
    {
        var bytes = EntityPackets.NpcWeaponAdd(name: "Tool", image: "tool.png", formattedClientGs1: "");

        Assert.Equal(
            new byte[] { 65, 36, 84, 111, 111, 108, 32, 40, 116, 111, 111, 108, 46, 112, 110, 103, 33, 32, 32, 10 },
            bytes);
    }

    [Fact]
    public void NpcWeaponDeleteAndRawScriptBytesMatchConfirmedCppPackets()
    {
        Assert.Equal(
            [66, 84, 111, 111, 108, 10],
            EntityPackets.NpcWeaponDelete("Tool"));

        Assert.Equal(
            [132, 32, 32, 35, 10, 172, 65, 66, 67],
            EntityPackets.NpcWeaponScriptRawData([65, 66, 67]));
    }

    [Fact]
    public void UpdateGaniParserReadsChecksumAndRemainingGaniName()
    {
        var writer = new GraalBinaryWriter();
        writer.WriteGChar((byte)PlayerToServerPacketId.UpdateGani);
        writer.WriteGInt5(0x01020304);
        writer.WriteBytes(Encoding.ASCII.GetBytes("walk"));

        var request = EntityPackets.ParseUpdateGani(writer.ToArray());

        Assert.Equal(0x01020304u, request.Checksum);
        Assert.Equal("walk", request.Gani);
        Assert.Equal("walk.gani", request.GaniFile);
    }

    [Fact]
    public void GaniScriptRawDataMatchesGameAniBytecodePacketShape()
    {
        Assert.Equal(
            [132, 32, 32, 40, 10, 166, 36, 119, 97, 108, 107, 65, 66, 67],
            EntityPackets.GaniScriptRawData("walk.gani", [65, 66, 67]));

        Assert.Equal(
            [132, 32, 32, 40, 10, 166, 36, 119, 97, 108, 107, 65, 66, 67],
            EntityPackets.GaniScriptRawData("walk", [65, 66, 67]));
    }

    [Fact]
    public void LoadGaniSetBackToMatchesPlayerScriptsPacketShape()
    {
        Assert.Equal(
            [227, 36, 119, 97, 108, 107, 34, 83, 69, 84, 66, 65, 67, 75, 84, 79, 32, 105, 100, 108, 101, 34, 10],
            EntityPackets.LoadGaniSetBackTo("walk", "idle"));
    }

    [Fact]
    public void GaniScriptIsSentOnlyWhenClientChecksumDiffers()
    {
        var bytecode = Encoding.ASCII.GetBytes("ABC");

        Assert.False(EntityPackets.ShouldSendGaniScript(bytecode, Crc32.Compute(bytecode)));
        Assert.True(EntityPackets.ShouldSendGaniScript(bytecode, 0));
    }

    [Fact]
    public void MissingClassScriptHeaderUsesConfirmedTokenizedEmptyBytecodeHeader()
    {
        Assert.Equal(
            [
                172, 0, 32,
                99, 108, 97, 115, 115, 44,
                102, 111, 111, 44,
                49, 44,
                34, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 34, 44,
                34, 32, 32, 32, 32, 32, 34,
                10
            ],
            EntityPackets.MissingClassScriptHeader("foo"));
    }

    [Fact]
    public void NpcDeleteAndNpcPropsUseGIntNpcIds()
    {
        Assert.Equal(new byte[] { 61, 32, 32, 39, 10 }, EntityPackets.NpcDelete(7));
        Assert.Equal(new byte[] { 35, 32, 32, 39, 70, 71, 10 }, EntityPackets.NpcProps(7, [70, 71]));
    }

    [Fact]
    public void NpcDelete2UsesLevelStringThenGIntNpcId()
    {
        var bytes = EntityPackets.NpcDelete2("start.nw", 7);

        Assert.Equal(
            new byte[] { 182, 115, 116, 97, 114, 116, 46, 110, 119, 32, 32, 39, 10 },
            bytes);
    }
}

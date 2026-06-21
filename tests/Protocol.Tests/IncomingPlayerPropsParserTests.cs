using Preagonal.GServer.Protocol;
using Xunit;

namespace Preagonal.GServer.Protocol.Tests;

public sealed class IncomingPlayerPropsParserTests
{
    [Fact]
    public void ParsesConfirmedMovementPropsFromPlayerPropsBody()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.X);
        body.WriteGChar(70);
        body.WriteGChar((byte)PlayerPropertyId.Y);
        body.WriteGChar(71);
        body.WriteGChar((byte)PlayerPropertyId.Z);
        body.WriteGChar(55);
        body.WriteGChar((byte)PlayerPropertyId.Sprite);
        body.WriteGChar(2);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.True(result.Success);
        Assert.Equal(4, result.Updates.Count);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.X && update.GCharValue == 70);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.Y && update.GCharValue == 71);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.Z && update.GCharValue == 55);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.Sprite && update.GCharValue == 2);
    }

    [Fact]
    public void ParsesConfirmedStatusByteWithoutApplyingDeathOrReviveSideEffects()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.Status);
        body.WriteGChar(4);
        body.WriteGChar((byte)PlayerPropertyId.X);
        body.WriteGChar(70);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.True(result.Success);
        Assert.Equal([PlayerPropertyId.Status, PlayerPropertyId.X], result.Updates.Select(update => update.PropertyId));
        Assert.Equal((byte)4, result.Updates[0].GCharValue);
        Assert.Equal((byte)70, result.Updates[1].GCharValue);
    }

    [Fact]
    public void ParsesConfirmedTerminalStatusWithoutPayloadAsEofGCharValue()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.Status);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.True(result.Success);
        var update = Assert.Single(result.Updates);
        Assert.Equal(PlayerPropertyId.Status, update.PropertyId);
        Assert.Equal((byte)224, update.GCharValue);
    }

    [Fact]
    public void ParsesConfirmedNicknameBytesWithoutApplyingWordFilterOrSetNickSideEffects()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.Nickname);
        body.WriteGChar(4);
        body.WriteBytes("Ruan"u8);
        body.WriteGChar((byte)PlayerPropertyId.X);
        body.WriteGChar(70);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.True(result.Success);
        Assert.Equal([PlayerPropertyId.Nickname, PlayerPropertyId.X], result.Updates.Select(update => update.PropertyId));
        Assert.Equal("Ruan", result.Updates[0].StringValue);
        Assert.Equal((byte)70, result.Updates[1].GCharValue);
    }

    [Fact]
    public void ParsesConfirmedNicknameByClampingDeclaredLengthToRemainingBytes()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.Nickname);
        body.WriteGChar(4);
        body.WriteBytes("Ru"u8);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.True(result.Success);
        var update = Assert.Single(result.Updates);
        Assert.Equal(PlayerPropertyId.Nickname, update.PropertyId);
        Assert.Equal("Ru", update.StringValue);
    }

    [Fact]
    public void ParsesConfirmedStringAndPreciseCoordinateProps()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.CurrentLevel);
        body.WriteGChar(8);
        body.WriteBytes("start.nw"u8);
        body.WriteGChar((byte)PlayerPropertyId.Gani);
        body.WriteGChar(4);
        body.WriteBytes("walk"u8);
        body.WriteGChar((byte)PlayerPropertyId.X2);
        body.WriteGShort(1120);
        body.WriteGChar((byte)PlayerPropertyId.Y2);
        body.WriteGShort(1121);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.True(result.Success);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.CurrentLevel && update.StringValue == "start.nw");
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.Gani && update.StringValue == "walk");
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.X2 && update.GShortValue == 1120);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.Y2 && update.GShortValue == 1121);
    }

    [Fact]
    public void ParsesConfirmedCurrentLevelByClampingDeclaredLengthToRemainingBytes()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.CurrentLevel);
        body.WriteGChar(8);
        body.WriteBytes("start"u8);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.True(result.Success);
        var update = Assert.Single(result.Updates);
        Assert.Equal(PlayerPropertyId.CurrentLevel, update.PropertyId);
        Assert.Equal("start", update.StringValue);
    }

    [Fact]
    public void ParsesConfirmedModernGaniByClampingDeclaredLengthToRemainingBytes()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.Gani);
        body.WriteGChar(4);
        body.WriteBytes("wa"u8);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray(), ClientVersionId.Client21);

        Assert.True(result.Success);
        var update = Assert.Single(result.Updates);
        Assert.Equal(PlayerPropertyId.Gani, update.PropertyId);
        Assert.Equal("wa", update.StringValue);
    }

    [Fact]
    public void StopsOnFirstInvalidUnknown77PropertyLikeCppSetPropsDefaultReturn()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.X);
        body.WriteGChar(70);
        body.WriteGChar(77);
        body.WriteGChar((byte)PlayerPropertyId.Y);
        body.WriteGChar(71);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.False(result.Success);
        Assert.Equal((PlayerPropertyId)77, result.UnsupportedPropertyId);
        Assert.Single(result.Updates);
        Assert.DoesNotContain(result.Updates, update => update.PropertyId == PlayerPropertyId.Y);
    }

    [Fact]
    public void ParsesConfirmedReadOnlyAndNoBytePropsWithoutInventingValues()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.Id);
        body.WriteGShort(7);
        body.WriteGChar((byte)PlayerPropertyId.KillsCount);
        body.WriteGInt(111);
        body.WriteGChar((byte)PlayerPropertyId.DeathsCount);
        body.WriteGInt(222);
        body.WriteGChar((byte)PlayerPropertyId.OnlineSeconds);
        body.WriteGInt(333);
        body.WriteGChar((byte)PlayerPropertyId.JoinLeaveLevel);
        body.WriteGChar((byte)PlayerPropertyId.PlayerConnected);
        body.WriteGChar((byte)PlayerPropertyId.Unknown81);
        body.WriteGChar(3);
        body.WriteGChar((byte)PlayerPropertyId.X);
        body.WriteGChar(70);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.True(result.Success);
        Assert.Equal(
            [
                PlayerPropertyId.Id,
                PlayerPropertyId.KillsCount,
                PlayerPropertyId.DeathsCount,
                PlayerPropertyId.OnlineSeconds,
                PlayerPropertyId.JoinLeaveLevel,
                PlayerPropertyId.PlayerConnected,
                PlayerPropertyId.Unknown81,
                PlayerPropertyId.X
            ],
            result.Updates.Select(update => update.PropertyId));
        Assert.All(result.Updates.Take(7), update =>
        {
            Assert.Null(update.GCharValue);
            Assert.Null(update.GShortValue);
            Assert.Null(update.StringValue);
        });
    }

    [Fact]
    public void ParsesConfirmedScalarInventoryAndStatProps()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.MaxPower);
        body.WriteGChar(15);
        body.WriteGChar((byte)PlayerPropertyId.CurrentPower);
        body.WriteGChar(11);
        body.WriteGChar((byte)PlayerPropertyId.RupeesCount);
        body.WriteGInt(3_000_000);
        body.WriteGChar((byte)PlayerPropertyId.ArrowsCount);
        body.WriteGChar(150);
        body.WriteGChar((byte)PlayerPropertyId.BombsCount);
        body.WriteGChar(151);
        body.WriteGChar((byte)PlayerPropertyId.GlovePower);
        body.WriteGChar(9);
        body.WriteGChar((byte)PlayerPropertyId.BombPower);
        body.WriteGChar(8);
        body.WriteGChar((byte)PlayerPropertyId.ApCounter);
        body.WriteGShort(123);
        body.WriteGChar((byte)PlayerPropertyId.MagicPoints);
        body.WriteGChar(200);
        body.WriteGChar((byte)PlayerPropertyId.AdditionalFlags);
        body.WriteGChar(77);
        body.WriteGChar((byte)PlayerPropertyId.Alignment);
        body.WriteGChar(120);
        body.WriteGChar((byte)PlayerPropertyId.CarrySprite);
        body.WriteGChar(12);
        body.WriteGChar((byte)PlayerPropertyId.HorseBushes);
        body.WriteGChar(6);
        body.WriteGChar((byte)PlayerPropertyId.PlayerStatusMessage);
        body.WriteGChar(4);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.True(result.Success);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.MaxPower && update.GCharValue == 15);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.CurrentPower && update.GCharValue == 11);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.RupeesCount && update.GIntValue == 3_000_000);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.ArrowsCount && update.GCharValue == 150);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.BombsCount && update.GCharValue == 151);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.GlovePower && update.GCharValue == 9);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.BombPower && update.GCharValue == 8);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.ApCounter && update.GShortValue == 123);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.MagicPoints && update.GCharValue == 200);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.AdditionalFlags && update.GCharValue == 77);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.Alignment && update.GCharValue == 120);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.CarrySprite && update.GCharValue == 12);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.HorseBushes && update.GCharValue == 6);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.PlayerStatusMessage && update.GCharValue == 4);
    }

    [Fact]
    public void ParsesConfirmedTerminalRupeesWithoutPayloadAsUnsignedClampedMaximum()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.RupeesCount);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.True(result.Success);
        var update = Assert.Single(result.Updates);
        Assert.Equal(PlayerPropertyId.RupeesCount, update.PropertyId);
        Assert.Equal(9_999_999, update.GIntValue);
    }

    [Fact]
    public void ParsesConfirmedTerminalArrowsWithoutPayloadAsEofGCharValue()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.ArrowsCount);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.True(result.Success);
        var update = Assert.Single(result.Updates);
        Assert.Equal(PlayerPropertyId.ArrowsCount, update.PropertyId);
        Assert.Equal((byte)224, update.GCharValue);
    }

    [Fact]
    public void ParsesConfirmedEnvironmentAndGaniAttributeProps()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.PlayerLanguage);
        body.WriteGChar(2);
        body.WriteBytes("pt"u8);
        body.WriteGChar((byte)PlayerPropertyId.OsType);
        body.WriteGChar(4);
        body.WriteBytes("wind"u8);
        body.WriteGChar((byte)PlayerPropertyId.TextCodePage);
        body.WriteGInt(1252);
        body.WriteGChar((byte)PlayerPropertyId.GAttrib1);
        body.WriteGChar(5);
        body.WriteBytes("sword"u8);
        body.WriteGChar((byte)PlayerPropertyId.GAttrib30);
        body.WriteGChar(4);
        body.WriteBytes("tail"u8);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.True(result.Success);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.PlayerLanguage && update.StringValue == "pt");
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.OsType && update.StringValue == "wind");
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.TextCodePage && update.GIntValue == 1252);
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.GAttrib1 && update.StringValue == "sword");
        Assert.Contains(result.Updates, update => update.PropertyId == PlayerPropertyId.GAttrib30 && update.StringValue == "tail");
    }

    [Fact]
    public void ParsesConfirmedEnvironmentStringsByClampingDeclaredLengthToRemainingBytes()
    {
        var languageBody = new GraalBinaryWriter();
        languageBody.WriteGChar((byte)PlayerPropertyId.PlayerLanguage);
        languageBody.WriteGChar(4);
        languageBody.WriteBytes("pt"u8);

        var osBody = new GraalBinaryWriter();
        osBody.WriteGChar((byte)PlayerPropertyId.OsType);
        osBody.WriteGChar(4);
        osBody.WriteBytes("wi"u8);

        var language = IncomingPlayerPropsParser.Parse(languageBody.ToArray());
        var os = IncomingPlayerPropsParser.Parse(osBody.ToArray());

        Assert.True(language.Success);
        Assert.True(os.Success);
        Assert.Equal("pt", Assert.Single(language.Updates).StringValue);
        Assert.Equal(PlayerPropertyId.PlayerLanguage, language.Updates[0].PropertyId);
        Assert.Equal("wi", Assert.Single(os.Updates).StringValue);
        Assert.Equal(PlayerPropertyId.OsType, os.Updates[0].PropertyId);
    }

    [Fact]
    public void ParsesConfirmedGaniAttributeByClampingDeclaredLengthToRemainingBytes()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.GAttrib1);
        body.WriteGChar(5);
        body.WriteBytes("sw"u8);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.True(result.Success);
        var update = Assert.Single(result.Updates);
        Assert.Equal(PlayerPropertyId.GAttrib1, update.PropertyId);
        Assert.Equal("sw", update.StringValue);
    }

    [Fact]
    public void ParsesConfirmedBodyImageProp()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.BodyImage);
        body.WriteGChar(8);
        body.WriteBytes("body.png"u8);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.True(result.Success);
        var update = Assert.Single(result.Updates);
        Assert.Equal(PlayerPropertyId.BodyImage, update.PropertyId);
        Assert.Equal("body.png", update.StringValue);
    }

    [Fact]
    public void ParsesConfirmedBodyImageByClampingDeclaredLengthToRemainingBytes()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.BodyImage);
        body.WriteGChar(8);
        body.WriteBytes("body"u8);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.True(result.Success);
        var update = Assert.Single(result.Updates);
        Assert.Equal(PlayerPropertyId.BodyImage, update.PropertyId);
        Assert.Equal("body", update.StringValue);
    }

    [Fact]
    public void ParsesConfirmedHorseImagePropForModernClientShape()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.HorseGif);
        body.WriteGChar(9);
        body.WriteBytes("horse.png"u8);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.True(result.Success);
        var update = Assert.Single(result.Updates);
        Assert.Equal(PlayerPropertyId.HorseGif, update.PropertyId);
        Assert.Equal("horse.png", update.StringValue);
    }

    [Fact]
    public void ParsesConfirmedOldClientHorseImageWithGifSuffix()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.HorseGif);
        body.WriteGChar(5);
        body.WriteBytes("horse"u8);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray(), ClientVersionId.Client1411);

        Assert.True(result.Success);
        var update = Assert.Single(result.Updates);
        Assert.Equal(PlayerPropertyId.HorseGif, update.PropertyId);
        Assert.Equal("horse.gif", update.StringValue);
    }

    [Fact]
    public void ParsesConfirmedHorseImageByReadingAtMost219Bytes()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.HorseGif);
        body.WriteGChar(222);
        body.WriteBytes(Enumerable.Repeat((byte)'h', 219).ToArray());
        body.WriteGChar((byte)PlayerPropertyId.X);
        body.WriteGChar(70);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray(), ClientVersionId.Client21);

        Assert.True(result.Success);
        Assert.Equal([PlayerPropertyId.HorseGif, PlayerPropertyId.X], result.Updates.Select(update => update.PropertyId));
        Assert.Equal(new string('h', 219), result.Updates[0].StringValue);
        Assert.Equal((byte)70, result.Updates[1].GCharValue);
    }

    [Fact]
    public void ParsesConfirmedHorseImageByClampingDeclaredLengthToRemainingBytes()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.HorseGif);
        body.WriteGChar(9);
        body.WriteBytes("horse"u8);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray(), ClientVersionId.Client21);

        Assert.True(result.Success);
        var update = Assert.Single(result.Updates);
        Assert.Equal(PlayerPropertyId.HorseGif, update.PropertyId);
        Assert.Equal("horse", update.StringValue);
    }

    [Fact]
    public void ParsesConfirmedCurrentChatByReadingAtMost223Bytes()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.CurrentChat);
        body.WriteGChar(226);
        body.WriteBytes(Enumerable.Repeat((byte)'c', 223).ToArray());
        body.WriteGChar((byte)PlayerPropertyId.X);
        body.WriteGChar(70);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray(), ClientVersionId.Client21);

        Assert.True(result.Success);
        Assert.Equal([PlayerPropertyId.CurrentChat, PlayerPropertyId.X], result.Updates.Select(update => update.PropertyId));
        Assert.Equal(new string('c', 223), result.Updates[0].StringValue);
        Assert.Equal((byte)70, result.Updates[1].GCharValue);
    }

    [Fact]
    public void ParsesConfirmedCurrentChatByClampingDeclaredLengthToRemainingBytes()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.CurrentChat);
        body.WriteGChar(8);
        body.WriteBytes("hello"u8);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray(), ClientVersionId.Client21);

        Assert.True(result.Success);
        var update = Assert.Single(result.Updates);
        Assert.Equal(PlayerPropertyId.CurrentChat, update.PropertyId);
        Assert.Equal("hello", update.StringValue);
    }

    [Fact]
    public void ParsesConfirmedAttachNpcByReadingObjectTypeAndNpcId()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.AttachNpc);
        body.WriteGChar(99);
        body.WriteGInt(123);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray(), ClientVersionId.Client21);

        Assert.True(result.Success);
        var update = Assert.Single(result.Updates);
        Assert.Equal(PlayerPropertyId.AttachNpc, update.PropertyId);
        Assert.Equal((byte)99, update.GCharValue);
        Assert.Equal(123, update.GIntValue);
    }

    [Fact]
    public void ParsesConfirmedTerminalAttachNpcWithoutPayloadAsEofObjectTypeAndUnsignedNpcId()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.AttachNpc);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray(), ClientVersionId.Client21);

        Assert.True(result.Success);
        var update = Assert.Single(result.Updates);
        Assert.Equal(PlayerPropertyId.AttachNpc, update.PropertyId);
        Assert.Equal((byte)224, update.GCharValue);
        Assert.Equal(4_294_438_880u, update.GUIntValue);
        Assert.Null(update.GIntValue);
    }

    [Fact]
    public void ParsesConfirmedCarryNpcByReadingGUIntWithoutApplyingOwnershipRules()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.CarryNpc);
        body.WriteGInt(123);
        body.WriteGChar((byte)PlayerPropertyId.X);
        body.WriteGChar(70);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray(), ClientVersionId.Client21);

        Assert.True(result.Success);
        Assert.Equal([PlayerPropertyId.CarryNpc, PlayerPropertyId.X], result.Updates.Select(update => update.PropertyId));
        Assert.Equal(123, result.Updates[0].GIntValue);
        Assert.Equal((byte)70, result.Updates[1].GCharValue);
    }

    [Fact]
    public void ParsesConfirmedTerminalCarryNpcWithoutPayloadAsUnsignedGIntValue()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.CarryNpc);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray(), ClientVersionId.Client21);

        Assert.True(result.Success);
        var update = Assert.Single(result.Updates);
        Assert.Equal(PlayerPropertyId.CarryNpc, update.PropertyId);
        Assert.Equal(4_294_438_880u, update.GUIntValue);
    }

    [Fact]
    public void ParsesConfirmedSwordAndShieldPowerRawValuesAndCustomImages()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.SwordPower);
        body.WriteGChar(35);
        body.WriteGChar(5);
        body.WriteBytes("slash"u8);
        body.WriteGChar((byte)PlayerPropertyId.ShieldPower);
        body.WriteGChar(12);
        body.WriteGChar(6);
        body.WriteBytes("guard1"u8);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray(), ClientVersionId.Client21);

        Assert.True(result.Success);
        Assert.Equal([PlayerPropertyId.SwordPower, PlayerPropertyId.ShieldPower], result.Updates.Select(update => update.PropertyId));
        Assert.Equal((byte)35, result.Updates[0].GCharValue);
        Assert.Equal("slash", result.Updates[0].StringValue);
        Assert.Equal((byte)12, result.Updates[1].GCharValue);
        Assert.Equal("guard1", result.Updates[1].StringValue);
    }

    [Fact]
    public void ParsesConfirmedOldClientSwordAndShieldCustomImagesWithGifSuffix()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.SwordPower);
        body.WriteGChar(35);
        body.WriteGChar(5);
        body.WriteBytes("slash"u8);
        body.WriteGChar((byte)PlayerPropertyId.ShieldPower);
        body.WriteGChar(12);
        body.WriteGChar(6);
        body.WriteBytes("guard1"u8);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray(), ClientVersionId.Client1411);

        Assert.True(result.Success);
        Assert.Equal("slash.gif", result.Updates[0].StringValue);
        Assert.Equal("guard1.gif", result.Updates[1].StringValue);
    }

    [Fact]
    public void ParsesConfirmedOldClientSwordAndShieldCustomImagesByClampingDeclaredLengthToRemainingBytes()
    {
        var swordBody = new GraalBinaryWriter();
        swordBody.WriteGChar((byte)PlayerPropertyId.SwordPower);
        swordBody.WriteGChar(35);
        swordBody.WriteGChar(5);
        swordBody.WriteBytes("sl"u8);

        var shieldBody = new GraalBinaryWriter();
        shieldBody.WriteGChar((byte)PlayerPropertyId.ShieldPower);
        shieldBody.WriteGChar(12);
        shieldBody.WriteGChar(6);
        shieldBody.WriteBytes("gu"u8);

        var sword = IncomingPlayerPropsParser.Parse(swordBody.ToArray(), ClientVersionId.Client1411);
        var shield = IncomingPlayerPropsParser.Parse(shieldBody.ToArray(), ClientVersionId.Client1411);

        Assert.True(sword.Success);
        Assert.True(shield.Success);
        Assert.Equal("sl.gif", Assert.Single(sword.Updates).StringValue);
        Assert.Equal((byte)35, sword.Updates[0].GCharValue);
        Assert.Equal("gu.gif", Assert.Single(shield.Updates).StringValue);
        Assert.Equal((byte)12, shield.Updates[0].GCharValue);
    }

    [Fact]
    public void ParsesConfirmedShieldClient141BugAsNoChangeWhenNoBytesRemain()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.ShieldPower);
        body.WriteGChar(11);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray(), ClientVersionId.Client1411);

        Assert.True(result.Success);
        var update = Assert.Single(result.Updates);
        Assert.Equal(PlayerPropertyId.ShieldPower, update.PropertyId);
        Assert.Null(update.GCharValue);
        Assert.Null(update.StringValue);
    }

    [Fact]
    public void ParsesConfirmedDefaultHeadImageNumberForModernAndOldClients()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.HeadGif);
        body.WriteGChar(25);

        var modern = IncomingPlayerPropsParser.Parse(body.ToArray(), ClientVersionId.Client21);
        var old = IncomingPlayerPropsParser.Parse(body.ToArray(), ClientVersionId.Client1411);

        Assert.True(modern.Success);
        Assert.True(old.Success);
        Assert.Equal("head25.png", Assert.Single(modern.Updates).StringValue);
        Assert.Equal("head25.gif", Assert.Single(old.Updates).StringValue);
    }

    [Fact]
    public void ParsesConfirmedCustomHeadImageOffsetAndOldClientGifSuffix()
    {
        var modernBody = new GraalBinaryWriter();
        modernBody.WriteGChar((byte)PlayerPropertyId.HeadGif);
        modernBody.WriteGChar(114);
        modernBody.WriteBytes("headcustom\nbad"u8);

        var oldBody = new GraalBinaryWriter();
        oldBody.WriteGChar((byte)PlayerPropertyId.HeadGif);
        oldBody.WriteGChar(104);
        oldBody.WriteBytes("head"u8);

        var modern = IncomingPlayerPropsParser.Parse(modernBody.ToArray(), ClientVersionId.Client21);
        var old = IncomingPlayerPropsParser.Parse(oldBody.ToArray(), ClientVersionId.Client1411);

        Assert.True(modern.Success);
        Assert.True(old.Success);
        Assert.Equal("headcustom", Assert.Single(modern.Updates).StringValue);
        Assert.Equal("head.gif", Assert.Single(old.Updates).StringValue);
    }

    [Fact]
    public void ParsesConfirmedCustomHeadImageKeepsLeadingNewline()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.HeadGif);
        body.WriteGChar(105);
        body.WriteBytes("\nhead"u8);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray(), ClientVersionId.Client21);

        Assert.True(result.Success);
        var update = Assert.Single(result.Updates);
        Assert.Equal(PlayerPropertyId.HeadGif, update.PropertyId);
        Assert.Equal("\nhead", update.StringValue);
    }

    [Fact]
    public void ParsesConfirmedCustomHeadImageByClampingDeclaredLengthToRemainingBytes()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.HeadGif);
        body.WriteGChar(108);
        body.WriteBytes("head"u8);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray(), ClientVersionId.Client21);

        Assert.True(result.Success);
        var update = Assert.Single(result.Updates);
        Assert.Equal(PlayerPropertyId.HeadGif, update.PropertyId);
        Assert.Equal("head", update.StringValue);
    }

    [Fact]
    public void ParsesConfirmedHeadImageNoChangeLengthWithoutInventingValue()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.HeadGif);
        body.WriteGChar(100);
        body.WriteGChar((byte)PlayerPropertyId.X);
        body.WriteGChar(70);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray(), ClientVersionId.Client21);

        Assert.True(result.Success);
        Assert.Equal([PlayerPropertyId.HeadGif, PlayerPropertyId.X], result.Updates.Select(update => update.PropertyId));
        Assert.Null(result.Updates[0].StringValue);
        Assert.Equal((byte)70, result.Updates[1].GCharValue);
    }

    [Fact]
    public void ParsesConfirmedColorPropAsFiveGChars()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.Colors);
        body.WriteGChar(1);
        body.WriteGChar(2);
        body.WriteGChar(3);
        body.WriteGChar(4);
        body.WriteGChar(5);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.True(result.Success);
        var update = Assert.Single(result.Updates);
        Assert.Equal(PlayerPropertyId.Colors, update.PropertyId);
        Assert.Equal([1, 2, 3, 4, 5], update.BytesValue);
    }

    [Fact]
    public void ParsesConfirmedEffectColorsByConsumingOptionalGInt4()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.EffectColors);
        body.WriteGChar(0);
        body.WriteGChar((byte)PlayerPropertyId.EffectColors);
        body.WriteGChar(1);
        body.WriteGInt4(0x01020304);
        body.WriteGChar((byte)PlayerPropertyId.X);
        body.WriteGChar(70);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.True(result.Success);
        Assert.Equal(
            [
                PlayerPropertyId.EffectColors,
                PlayerPropertyId.EffectColors,
                PlayerPropertyId.X
            ],
            result.Updates.Select(update => update.PropertyId));
        Assert.All(result.Updates.Take(2), update =>
        {
            Assert.Null(update.GCharValue);
            Assert.Null(update.GIntValue);
            Assert.Null(update.BytesValue);
        });
        Assert.Equal((byte)70, result.Updates[2].GCharValue);
    }

    [Fact]
    public void ParsesConfirmedRatingByConsumingGIntWithoutInventingMutationValue()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.Rating);
        body.WriteGInt(123456);
        body.WriteGChar((byte)PlayerPropertyId.X);
        body.WriteGChar(70);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.True(result.Success);
        Assert.Equal([PlayerPropertyId.Rating, PlayerPropertyId.X], result.Updates.Select(update => update.PropertyId));
        Assert.Null(result.Updates[0].GIntValue);
        Assert.Null(result.Updates[0].GCharValue);
        Assert.Equal((byte)70, result.Updates[1].GCharValue);
    }

    [Fact]
    public void ParsesConfirmedAccountNameByConsumingStringWithoutInventingMutationValue()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.AccountName);
        body.WriteGChar(4);
        body.WriteBytes("Ruan"u8);
        body.WriteGChar((byte)PlayerPropertyId.X);
        body.WriteGChar(70);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.True(result.Success);
        Assert.Equal([PlayerPropertyId.AccountName, PlayerPropertyId.X], result.Updates.Select(update => update.PropertyId));
        Assert.Null(result.Updates[0].StringValue);
        Assert.Null(result.Updates[0].GCharValue);
        Assert.Equal((byte)70, result.Updates[1].GCharValue);
    }

    [Fact]
    public void ParsesConfirmedCommunityNameByConsumingStringWithoutInventingMutationValue()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.CommunityName);
        body.WriteGChar(8);
        body.WriteBytes("commname"u8);
        body.WriteGChar((byte)PlayerPropertyId.X);
        body.WriteGChar(70);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.True(result.Success);
        Assert.Equal([PlayerPropertyId.CommunityName, PlayerPropertyId.X], result.Updates.Select(update => update.PropertyId));
        Assert.Null(result.Updates[0].StringValue);
        Assert.Null(result.Updates[0].GCharValue);
        Assert.Equal((byte)70, result.Updates[1].GCharValue);
    }

    [Fact]
    public void ParsesConfirmedConsumeOnlyNamesByClampingDeclaredLengthToRemainingBytes()
    {
        var accountBody = new GraalBinaryWriter();
        accountBody.WriteGChar((byte)PlayerPropertyId.AccountName);
        accountBody.WriteGChar(4);
        accountBody.WriteBytes("Ru"u8);

        var communityBody = new GraalBinaryWriter();
        communityBody.WriteGChar((byte)PlayerPropertyId.CommunityName);
        communityBody.WriteGChar(8);
        communityBody.WriteBytes("comm"u8);

        var account = IncomingPlayerPropsParser.Parse(accountBody.ToArray());
        var community = IncomingPlayerPropsParser.Parse(communityBody.ToArray());

        Assert.True(account.Success);
        Assert.True(community.Success);
        Assert.Equal(PlayerPropertyId.AccountName, Assert.Single(account.Updates).PropertyId);
        Assert.Equal(PlayerPropertyId.CommunityName, Assert.Single(community.Updates).PropertyId);
        Assert.Null(account.Updates[0].StringValue);
        Assert.Null(community.Updates[0].StringValue);
    }

    [Fact]
    public void ParsesConfirmedIpAddressByConsumingGInt5WithoutInventingMutationValue()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.IpAddress);
        body.WriteGInt5(0x7F000001);
        body.WriteGChar((byte)PlayerPropertyId.X);
        body.WriteGChar(70);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.True(result.Success);
        Assert.Equal([PlayerPropertyId.IpAddress, PlayerPropertyId.X], result.Updates.Select(update => update.PropertyId));
        Assert.Null(result.Updates[0].GIntValue);
        Assert.Null(result.Updates[0].GCharValue);
        Assert.Equal((byte)70, result.Updates[1].GCharValue);
    }

    [Fact]
    public void ParsesConfirmedUdpPortAsGInt()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.UdpPort);
        body.WriteGInt(14900);
        body.WriteGChar((byte)PlayerPropertyId.X);
        body.WriteGChar(70);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.True(result.Success);
        Assert.Equal([PlayerPropertyId.UdpPort, PlayerPropertyId.X], result.Updates.Select(update => update.PropertyId));
        Assert.Equal(14900, result.Updates[0].GIntValue);
        Assert.Equal((byte)70, result.Updates[1].GCharValue);
    }

    [Fact]
    public void ParsesConfirmedGmapLevelCoordinatesAsGChars()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.GmapLevelX);
        body.WriteGChar(4);
        body.WriteGChar((byte)PlayerPropertyId.GmapLevelY);
        body.WriteGChar(5);
        body.WriteGChar((byte)PlayerPropertyId.X);
        body.WriteGChar(70);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray());

        Assert.True(result.Success);
        Assert.Equal([PlayerPropertyId.GmapLevelX, PlayerPropertyId.GmapLevelY, PlayerPropertyId.X], result.Updates.Select(update => update.PropertyId));
        Assert.Equal((byte)4, result.Updates[0].GCharValue);
        Assert.Equal((byte)5, result.Updates[1].GCharValue);
        Assert.Equal((byte)70, result.Updates[2].GCharValue);
    }

    [Fact]
    public void BuildsConfirmedForwardedMovementPropsForPreciseSender()
    {
        var updates = new[]
        {
            IncomingPlayerPropertyUpdate.GShort(PlayerPropertyId.X2, 1120),
            IncomingPlayerPropertyUpdate.GShort(PlayerPropertyId.Y2, 1120)
        };

        var packet = IncomingPlayerPropsForwarding.BuildOtherPlayerPropsPacket(
            playerId: 7,
            pixelX: 560,
            pixelY: 560,
            pixelZ: 0,
            updates,
            senderSupportsPreciseMovement: true,
            appendNewline: true);

        Assert.Equal(
            new byte[]
            {
                40, 32, 39,
                47, 102,
                48, 102,
                110, 40, 128,
                111, 40, 128,
                10
            },
            packet);
    }

    [Fact]
    public void BuildsConfirmedForwardedLegacyXYPropsWithPreciseMirrorsForPreciseSender()
    {
        var updates = new[]
        {
            IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.X, 70),
            IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.Y, 71)
        };

        var packet = IncomingPlayerPropsForwarding.BuildOtherPlayerPropsPacket(
            playerId: 7,
            pixelX: 560,
            pixelY: 568,
            pixelZ: 0,
            updates,
            senderSupportsPreciseMovement: true,
            appendNewline: true);

        Assert.Equal(
            new byte[]
            {
                40, 32, 39,
                110, 40, 128,
                111, 40, 144,
                47, 102,
                48, 103,
                10
            },
            packet);
    }

    [Fact]
    public void BuildsConfirmedForwardedLegacyXYPropsWithPreciseMirrorsForOlderSender()
    {
        var updates = new[]
        {
            IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.X, 70),
            IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.Y, 71)
        };

        var packet = IncomingPlayerPropsForwarding.BuildOtherPlayerPropsPacket(
            playerId: 7,
            pixelX: 560,
            pixelY: 568,
            pixelZ: 0,
            updates,
            senderSupportsPreciseMovement: false,
            appendNewline: true);

        Assert.Equal(
            new byte[]
            {
                40, 32, 39,
                47, 102,
                48, 103,
                110, 40, 128,
                111, 40, 144,
                10
            },
            packet);
    }

    [Fact]
    public void BuildsConfirmedForwardedPreciseZPropWithLegacyMirrorForPreciseSender()
    {
        var updates = new[]
        {
            IncomingPlayerPropertyUpdate.GShort(PlayerPropertyId.Z2, 79)
        };

        var packet = IncomingPlayerPropsForwarding.BuildOtherPlayerPropsPacket(
            playerId: 7,
            pixelX: 0,
            pixelY: 0,
            pixelZ: -39,
            updates,
            senderSupportsPreciseMovement: true,
            appendNewline: true);

        Assert.Equal(
            new byte[]
            {
                40, 32, 39,
                77, 78,
                112, 32, 111,
                10
            },
            packet);
    }

    [Fact]
    public void BuildsConfirmedForwardedPreciseZPropWithLegacyMirrorForOlderSender()
    {
        var updates = new[]
        {
            IncomingPlayerPropertyUpdate.GShort(PlayerPropertyId.Z2, 79)
        };

        var packet = IncomingPlayerPropsForwarding.BuildOtherPlayerPropsPacket(
            playerId: 7,
            pixelX: 0,
            pixelY: 0,
            pixelZ: -39,
            updates,
            senderSupportsPreciseMovement: false,
            appendNewline: true);

        Assert.Equal(
            new byte[]
            {
                40, 32, 39,
                112, 32, 111,
                77, 78,
                10
            },
            packet);
    }

    [Fact]
    public void BuildsConfirmedForwardedLegacyZPropWithPreciseMirrorForPreciseSender()
    {
        var updates = new[]
        {
            IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.Z, 46)
        };

        var packet = IncomingPlayerPropsForwarding.BuildOtherPlayerPropsPacket(
            playerId: 7,
            pixelX: 0,
            pixelY: 0,
            pixelZ: -32,
            updates,
            senderSupportsPreciseMovement: true,
            appendNewline: true);

        Assert.Equal(
            new byte[]
            {
                40, 32, 39,
                112, 32, 97,
                77, 78,
                10
            },
            packet);
    }

    [Fact]
    public void BuildsConfirmedForwardedLegacyZPropWithPreciseMirrorForOlderSender()
    {
        var updates = new[]
        {
            IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.Z, 46)
        };

        var packet = IncomingPlayerPropsForwarding.BuildOtherPlayerPropsPacket(
            playerId: 7,
            pixelX: 0,
            pixelY: 0,
            pixelZ: -32,
            updates,
            senderSupportsPreciseMovement: false,
            appendNewline: true);

        Assert.Equal(
            new byte[]
            {
                40, 32, 39,
                77, 78,
                112, 32, 97,
                10
            },
            packet);
    }

    [Fact]
    public void BuildsConfirmedForwardedMovementPropsForOlderSender()
    {
        var updates = new[]
        {
            IncomingPlayerPropertyUpdate.GShort(PlayerPropertyId.X2, 1120),
            IncomingPlayerPropertyUpdate.GShort(PlayerPropertyId.Y2, 1120)
        };

        var packet = IncomingPlayerPropsForwarding.BuildOtherPlayerPropsPacket(
            playerId: 7,
            pixelX: 560,
            pixelY: 560,
            pixelZ: 0,
            updates,
            senderSupportsPreciseMovement: false,
            appendNewline: true);

        Assert.Equal(
            new byte[]
            {
                40, 32, 39,
                110, 40, 128,
                111, 40, 128,
                47, 102,
                48, 102,
                10
            },
            packet);
    }

    [Fact]
    public void DoesNotForwardConfirmedReadOnlyNoLocalProps()
    {
        var updates = new[]
        {
            IncomingPlayerPropertyUpdate.NoValue(PlayerPropertyId.KillsCount),
            IncomingPlayerPropertyUpdate.NoValue(PlayerPropertyId.DeathsCount),
            IncomingPlayerPropertyUpdate.NoValue(PlayerPropertyId.OnlineSeconds),
            IncomingPlayerPropertyUpdate.NoValue(PlayerPropertyId.Unknown81)
        };

        var packet = IncomingPlayerPropsForwarding.BuildOtherPlayerPropsPacket(
            playerId: 7,
            pixelX: 0,
            pixelY: 0,
            pixelZ: 0,
            updates,
            senderSupportsPreciseMovement: true,
            appendNewline: true);

        Assert.Equal([40, 32, 39, 10], packet);
    }

    [Fact]
    public void DoesNotForwardConfirmedEffectColorsBecauseSendLocalIsFalse()
    {
        var packet = IncomingPlayerPropsForwarding.BuildOtherPlayerPropsPacket(
            playerId: 7,
            pixelX: 0,
            pixelY: 0,
            pixelZ: 0,
            [IncomingPlayerPropertyUpdate.NoValue(PlayerPropertyId.EffectColors)],
            senderSupportsPreciseMovement: true,
            appendNewline: true);

        Assert.Equal([40, 32, 39, 10], packet);
    }

    [Fact]
    public void ForwardsConfirmedApCounterUsingCppGetPropPlusOneBehavior()
    {
        var packet = IncomingPlayerPropsForwarding.BuildOtherPlayerPropsPacket(
            playerId: 7,
            pixelX: 0,
            pixelY: 0,
            pixelZ: 0,
            [IncomingPlayerPropertyUpdate.GShort(PlayerPropertyId.ApCounter, 123)],
            senderSupportsPreciseMovement: false,
            appendNewline: true);

        Assert.Equal([40, 32, 39, 57, 32, 156, 10], packet);
    }

    [Fact]
    public void ForwardsConfirmedMaxPowerAsCurrentPowerLikeNonV8CppBranch()
    {
        var packet = IncomingPlayerPropsForwarding.BuildOtherPlayerPropsPacket(
            playerId: 7,
            pixelX: 0,
            pixelY: 0,
            pixelZ: 0,
            [IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.MaxPower, 15)],
            senderSupportsPreciseMovement: true,
            appendNewline: true);

        Assert.Equal([40, 32, 39, 34, 62, 10], packet);
    }

    [Fact]
    public void ForwardsConfirmedLocalScalarPropsUsingCppSendLocalTable()
    {
        var packet = IncomingPlayerPropsForwarding.BuildOtherPlayerPropsPacket(
            playerId: 7,
            pixelX: 0,
            pixelY: 0,
            pixelZ: 0,
            [
                IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.CarrySprite, 12),
                IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.MagicPoints, 88),
                IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.Alignment, 120),
                IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.AdditionalFlags, 77),
                IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.HorseBushes, 6)
            ],
            senderSupportsPreciseMovement: true,
            appendNewline: true);

        Assert.Equal([40, 32, 39, 51, 44, 64, 132, 10], packet);
    }

    [Fact]
    public void ForwardsConfirmedUdpPortUsingGenericSendLocalTail()
    {
        var packet = IncomingPlayerPropsForwarding.BuildOtherPlayerPropsPacket(
            playerId: 7,
            pixelX: 0,
            pixelY: 0,
            pixelZ: 0,
            [IncomingPlayerPropertyUpdate.GInt(PlayerPropertyId.UdpPort, 14900)],
            senderSupportsPreciseMovement: true,
            appendNewline: true);

        Assert.Equal([40, 32, 39, 63, 32, 148, 84, 10], packet);
    }

    [Fact]
    public void ForwardsConfirmedGaniAttributesWithOriginalStringPayload()
    {
        var packet = IncomingPlayerPropsForwarding.BuildOtherPlayerPropsPacket(
            playerId: 7,
            pixelX: 0,
            pixelY: 0,
            pixelZ: 0,
            [IncomingPlayerPropertyUpdate.String(PlayerPropertyId.GAttrib1, "sword")],
            senderSupportsPreciseMovement: true,
            appendNewline: true);

        Assert.Equal([40, 32, 39, 69, 37, 115, 119, 111, 114, 100, 10], packet);
    }

    [Fact]
    public void ForwardsConfirmedColorsWithFiveGChars()
    {
        var packet = IncomingPlayerPropsForwarding.BuildOtherPlayerPropsPacket(
            playerId: 7,
            pixelX: 0,
            pixelY: 0,
            pixelZ: 0,
            [IncomingPlayerPropertyUpdate.Bytes(PlayerPropertyId.Colors, [1, 2, 3, 4, 5])],
            senderSupportsPreciseMovement: true,
            appendNewline: true);

        Assert.Equal([40, 32, 39, 45, 33, 34, 35, 36, 37, 10], packet);
    }

    [Fact]
    public void ForwardsConfirmedBodyImageWithCurrentStringPayload()
    {
        var packet = IncomingPlayerPropsForwarding.BuildOtherPlayerPropsPacket(
            playerId: 7,
            pixelX: 0,
            pixelY: 0,
            pixelZ: 0,
            [IncomingPlayerPropertyUpdate.String(PlayerPropertyId.BodyImage, "body.png")],
            senderSupportsPreciseMovement: true,
            appendNewline: true);

        Assert.Equal([40, 32, 39, 67, 40, 98, 111, 100, 121, 46, 112, 110, 103, 10], packet);
    }

    [Fact]
    public void ForwardsConfirmedHeadImageWithCppLengthPlusHundredPayload()
    {
        var packet = IncomingPlayerPropsForwarding.BuildOtherPlayerPropsPacket(
            playerId: 7,
            pixelX: 0,
            pixelY: 0,
            pixelZ: 0,
            [IncomingPlayerPropertyUpdate.String(PlayerPropertyId.HeadGif, "head.png")],
            senderSupportsPreciseMovement: true,
            appendNewline: true);

        Assert.Equal([40, 32, 39, 43, 140, 104, 101, 97, 100, 46, 112, 110, 103, 10], packet);
    }

    [Fact]
    public void ForwardsConfirmedHorseImageWithCurrentStringPayload()
    {
        var packet = IncomingPlayerPropsForwarding.BuildOtherPlayerPropsPacket(
            playerId: 7,
            pixelX: 0,
            pixelY: 0,
            pixelZ: 0,
            [IncomingPlayerPropertyUpdate.String(PlayerPropertyId.HorseGif, "horse.png")],
            senderSupportsPreciseMovement: true,
            appendNewline: true);

        Assert.Equal([40, 32, 39, 53, 41, 104, 111, 114, 115, 101, 46, 112, 110, 103, 10], packet);
    }

    [Fact]
    public void ForwardsConfirmedCurrentChatWithCurrentStringPayload()
    {
        var packet = IncomingPlayerPropsForwarding.BuildOtherPlayerPropsPacket(
            playerId: 7,
            pixelX: 0,
            pixelY: 0,
            pixelZ: 0,
            [IncomingPlayerPropertyUpdate.String(PlayerPropertyId.CurrentChat, "hello")],
            senderSupportsPreciseMovement: true,
            appendNewline: true);

        Assert.Equal([40, 32, 39, 44, 37, 104, 101, 108, 108, 111, 10], packet);
    }

    [Fact]
    public void ForwardsConfirmedAttachNpcWithNpcObjectTypeZero()
    {
        var packet = IncomingPlayerPropsForwarding.BuildOtherPlayerPropsPacket(
            playerId: 7,
            pixelX: 0,
            pixelY: 0,
            pixelZ: 0,
            [new IncomingPlayerPropertyUpdate(PlayerPropertyId.AttachNpc, GCharValue: 99, GIntValue: 123)],
            senderSupportsPreciseMovement: true,
            appendNewline: true);

        Assert.Equal([40, 32, 39, 74, 32, 32, 32, 155, 10], packet);
    }

    [Fact]
    public void ForwardsConfirmedSwordAndShieldWithCppPowerOffsetsAndImages()
    {
        var packet = IncomingPlayerPropsForwarding.BuildOtherPlayerPropsPacket(
            playerId: 7,
            pixelX: 0,
            pixelY: 0,
            pixelZ: 0,
            [
                new IncomingPlayerPropertyUpdate(PlayerPropertyId.SwordPower, GCharValue: 2, StringValue: "sword2.png"),
                new IncomingPlayerPropertyUpdate(PlayerPropertyId.ShieldPower, GCharValue: 1, StringValue: "shield1.png")
            ],
            senderSupportsPreciseMovement: true,
            appendNewline: true);

        Assert.Equal(
            [
                40, 32, 39,
                40, 64, 42, 115, 119, 111, 114, 100, 50, 46, 112, 110, 103,
                41, 43, 43, 115, 104, 105, 101, 108, 100, 49, 46, 112, 110, 103,
                10
            ],
            packet);
    }

    [Fact]
    public void OldClientGaniParsesBowPowerInsteadOfModernString()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.Gani);
        body.WriteGChar(4);
        body.WriteGChar((byte)PlayerPropertyId.X);
        body.WriteGChar(70);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray(), ClientVersionId.Client1411);

        Assert.True(result.Success);
        Assert.Equal([PlayerPropertyId.Gani, PlayerPropertyId.X], result.Updates.Select(update => update.PropertyId));
        Assert.Equal((byte)4, result.Updates[0].GCharValue);
        Assert.Null(result.Updates[0].StringValue);
        Assert.Equal((byte)70, result.Updates[1].GCharValue);
    }

    [Fact]
    public void OldClientGaniParsesBowImageAndAddsGifWhenExtensionless()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.Gani);
        body.WriteGChar(14);
        body.WriteBytes("bow1"u8);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray(), ClientVersionId.Client1411);

        Assert.True(result.Success);
        var update = Assert.Single(result.Updates);
        Assert.Equal(PlayerPropertyId.Gani, update.PropertyId);
        Assert.Equal((byte)10, update.GCharValue);
        Assert.Equal("bow1.gif", update.StringValue);
    }

    [Fact]
    public void OldClientGaniParsesTruncatedBowImageAndAddsGifWhenExtensionless()
    {
        var body = new GraalBinaryWriter();
        body.WriteGChar((byte)PlayerPropertyId.Gani);
        body.WriteGChar(14);
        body.WriteBytes("bo"u8);

        var result = IncomingPlayerPropsParser.Parse(body.ToArray(), ClientVersionId.Client1411);

        Assert.True(result.Success);
        var update = Assert.Single(result.Updates);
        Assert.Equal(PlayerPropertyId.Gani, update.PropertyId);
        Assert.Equal((byte)10, update.GCharValue);
        Assert.Equal("bo.gif", update.StringValue);
    }

    [Fact]
    public void OldClientGaniForwardsBowPowerPayload()
    {
        var packet = IncomingPlayerPropsForwarding.BuildOtherPlayerPropsPacket(
            playerId: 7,
            pixelX: 0,
            pixelY: 0,
            pixelZ: 0,
            [IncomingPlayerPropertyUpdate.GChar(PlayerPropertyId.Gani, 4)],
            senderSupportsPreciseMovement: true,
            appendNewline: true,
            senderClientVersion: ClientVersionId.Client1411);

        Assert.Equal([40, 32, 39, 42, 36, 10], packet);
    }

    [Fact]
    public void OldClientGaniForwardsBowImageWithLengthOffset()
    {
        var packet = IncomingPlayerPropsForwarding.BuildOtherPlayerPropsPacket(
            playerId: 7,
            pixelX: 0,
            pixelY: 0,
            pixelZ: 0,
            [new IncomingPlayerPropertyUpdate(PlayerPropertyId.Gani, GCharValue: 10, StringValue: "bow1.gif")],
            senderSupportsPreciseMovement: true,
            appendNewline: true,
            senderClientVersion: ClientVersionId.Client1411);

        Assert.Equal([40, 32, 39, 42, 50, 98, 111, 119, 49, 46, 103, 105, 102, 10], packet);
    }
}

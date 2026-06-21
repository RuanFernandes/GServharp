using Preagonal.GServer.Persistence;
using Xunit;

namespace Preagonal.GServer.Persistence.Tests;

public sealed class AccountFileParserTests
{
    [Fact]
    public void ParseRejectsMissingOrInvalidGraccHeader()
    {
        Assert.False(AccountFileParser.Parse("pc:Ruan", "").Success);
        Assert.False(AccountFileParser.Parse("pc:Ruan", "GRACC000\nNICK Ruan").Success);
    }

    [Fact]
    public void ParseUsesCppMemberDefaultsWhenFieldsAreMissing()
    {
        var result = AccountFileParser.Parse("pc:Ruan", "GRACC001\n");

        Assert.True(result.Success);
        var account = result.Account!;
        Assert.Equal("pc:Ruan", account.AccountName);
        Assert.Equal("pc:Ruan", account.CommunityName);
        Assert.Equal("default", account.Nickname);
        Assert.Equal(3, account.MaxHitpoints);
        Assert.Equal(3.0f, account.Hitpoints);
        Assert.Equal(10, account.Bombs);
        Assert.Equal(5, account.Arrows);
        Assert.Equal(1, account.GlovePower);
        Assert.Equal(1, account.SwordPower);
        Assert.Equal(1, account.ShieldPower);
        Assert.Equal(2, account.Sprite);
        Assert.Equal(50, account.Alignment);
        Assert.Equal(new byte[] { 2, 0, 10, 4, 18 }, account.Colors);
        Assert.Equal("idle", account.Gani);
        Assert.Equal("head0.png", account.HeadImage);
        Assert.Equal("body.png", account.BodyImage);
        Assert.Equal("sword1.png", account.SwordImage);
        Assert.Equal("shield1.png", account.ShieldImage);
        Assert.Equal("English", account.Language);
        Assert.Equal("0.0.0.0", account.AdminIp);
        Assert.Equal(20, account.Status);
    }

    [Fact]
    public void ParseReadsLoginFieldsAndMirrorsCppTruncationAndClipping()
    {
        var content = string.Join(
            "\n",
            "GRACC001",
            $"NICK {new string('n', 230)}",
            $"HEAD {new string('h', 130)}",
            $"BODY {new string('b', 230)}",
            $"SWORD {new string('s', 230)}",
            $"SHIELD {new string('d', 230)}",
            $"ANI {new string('a', 230)}",
            "LEVEL start.nw",
            "X 30.5",
            "Y 31.25",
            "Z 1.5",
            "MAXHP 99",
            "HP 99",
            "RUPEES 1234",
            "ARROWS 44",
            "BOMBS 55",
            "GLOVEP 2",
            "SHIELDP 8",
            "SWORDP 8",
            "COLORS 1,2,3,4,5,6",
            "SPRITE 9",
            "STATUS 7",
            "MP 6",
            "AP 77",
            "APCOUNTER 8",
            "ONSECS 99",
            "IP 12345",
            "LANGUAGE ",
            "KILLS 11",
            "DEATHS 12",
            "RATING 1600.5",
            "DEVIATION 321.5",
            "BANNED 1",
            "BANREASON cheating",
            "LOCALRIGHTS 16384",
            "IPRANGE 127.0.0.*",
            "LOADONLY 1",
            "ATTR1 attr-one",
            "ATTR30 attr-thirty",
            "FLAG client.flag=value");

        var result = AccountFileParser.Parse("pc:Ruan", content);

        Assert.True(result.Success);
        var account = result.Account!;
        Assert.Equal(223, account.Nickname.Length);
        Assert.Equal(123, account.HeadImage.Length);
        Assert.Equal(223, account.BodyImage.Length);
        Assert.Equal(223, account.SwordImage.Length);
        Assert.Equal(223, account.ShieldImage.Length);
        Assert.Equal(223, account.Gani.Length);
        Assert.Equal("start.nw", account.LevelName);
        Assert.Equal(488, account.PixelX);
        Assert.Equal(500, account.PixelY);
        Assert.Equal(24, account.PixelZ);
        Assert.Equal(3, account.MaxHitpoints);
        Assert.Equal(3.0f, account.Hitpoints);
        Assert.Equal(1234, account.Rupees);
        Assert.Equal(44, account.Arrows);
        Assert.Equal(55, account.Bombs);
        Assert.Equal(2, account.GlovePower);
        Assert.Equal(3, account.ShieldPower);
        Assert.Equal(3, account.SwordPower);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, account.Colors);
        Assert.Equal("English", account.Language);
        Assert.True(account.IsBanned);
        Assert.True(account.IsLoadOnly);
        Assert.Equal("cheating", account.BanReason);
        Assert.Equal(16384, account.AdminRights);
        Assert.Equal("127.0.0.*", account.AdminIp);
        Assert.Equal("attr-one", account.GaniAttributes[0]);
        Assert.Equal("attr-thirty", account.GaniAttributes[29]);
        Assert.Equal("value", account.Flags["client.flag"]);
    }

    [Fact]
    public void ParseCanIgnoreNicknameForRcAndNcLogin()
    {
        var result = AccountFileParser.Parse("pc:Ruan", "GRACC001\nNICK Other", ignoreNickname: true);

        Assert.True(result.Success);
        Assert.Equal("default", result.Account!.Nickname);
    }
}

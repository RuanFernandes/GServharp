using Preagonal.GServer.Persistence;
using Xunit;

namespace Preagonal.GServer.Persistence.Tests;

public sealed class AccountFileSerializerTests
{
    [Fact]
    public void SerializeWritesConfirmedSaveAccountOrderWithCrlf()
    {
        var account = CreatePopulatedAccount();

        var result = AccountFileSerializer.Serialize(account);

        Assert.True(result.Success);
        Assert.Equal(ExpectedPopulatedAccountText(), result.Contents);
        Assert.DoesNotContain("\n", result.Contents.Replace("\r\n", string.Empty, StringComparison.Ordinal));
    }

    [Fact]
    public void SerializeRefusesLoadOnlyAccountLikeCppSaveAccount()
    {
        var account = CreatePopulatedAccount();
        account.IsLoadOnly = true;

        var result = AccountFileSerializer.Serialize(account);

        Assert.False(result.Success);
        Assert.Equal(string.Empty, result.Contents);
    }

    [Fact]
    public void SaveUsesCasePreservedExistingFilenameWhenPresent()
    {
        var account = CreatePopulatedAccount();
        var fileSystem = new MemoryPersistenceFileSystem(@"C:\GServer\");
        fileSystem.ExistingName = "PC-Ruan.TXT";

        var result = AccountSaveService.Save(account, fileSystem);

        Assert.True(result.CppReturnValue);
        Assert.True(result.WriteAttempted);
        Assert.True(result.WriteSucceeded);
        Assert.Equal(@"C:\GServer\accounts\PC-Ruan.TXT", result.Path);
        Assert.Equal(ExpectedPopulatedAccountText(), fileSystem.WrittenContents);
        Assert.Null(result.AccountFileAdded);
    }

    [Fact]
    public void SaveReturnsCppSuccessEvenWhenDiskWriteFailsAfterSerialization()
    {
        var account = CreatePopulatedAccount();
        var fileSystem = new MemoryPersistenceFileSystem(@"C:\GServer\") { WriteResult = false };

        var result = AccountSaveService.Save(account, fileSystem);

        Assert.True(result.CppReturnValue);
        Assert.True(result.WriteAttempted);
        Assert.False(result.WriteSucceeded);
        Assert.Equal(@"C:\GServer\accounts\pc:Ruan.txt", result.Path);
    }

    [Fact]
    public void SaveCreatedDefaultAccountAddsSourceConfirmedRelativePath()
    {
        var account = CreatePopulatedAccount();
        account.AccountName = "NewAccount";
        var fileSystem = new MemoryPersistenceFileSystem(@"C:\GServer\");

        var result = AccountSaveService.SaveCreatedDefaultAccount(account, fileSystem, "NewAccount");

        Assert.True(result.CppReturnValue);
        Assert.Equal(@"accounts/NewAccount.txt", result.AccountFileAdded);
        Assert.Equal(@"accounts/NewAccount.txt", fileSystem.AddedFile);
    }

    private static AccountFileData CreatePopulatedAccount()
    {
        var account = new AccountFileData
        {
            AccountName = "pc:Ruan",
            CommunityName = "ignored-on-save",
            Nickname = "Ruan",
            LevelName = "start.nw",
            PixelX = 480,
            PixelY = 488,
            PixelZ = 24,
            MaxHitpoints = 5,
            Hitpoints = 4.5f,
            Rupees = 12,
            Gani = "walk",
            Arrows = 7,
            Bombs = 8,
            GlovePower = 2,
            ShieldPower = 3,
            SwordPower = 3,
            BowPower = 2,
            BowImage = "bow2.png",
            HeadImage = "head1.png",
            BodyImage = "body2.png",
            SwordImage = "sword3.png",
            ShieldImage = "shield3.png",
            Sprite = 4,
            Status = 1,
            MagicPoints = 6,
            Alignment = 77,
            ApCounter = 2,
            OnlineSeconds = 9,
            AccountIp = 123,
            Language = "Portuguese",
            Kills = 10,
            Deaths = 11,
            EloRating = 1600.0f,
            EloDeviation = 320.0f,
            LastSparTime = 42,
            IsBanned = true,
            BanReason = "reason",
            BanLength = "forever",
            Comments = "comment",
            Email = "ruan@example.test",
            AdminRights = 16384,
            AdminIp = "127.0.0.*",
            LastFolder = "levels/"
        };

        account.Colors[0] = 1;
        account.Colors[1] = 2;
        account.Colors[2] = 3;
        account.Colors[3] = 4;
        account.Colors[4] = 5;
        account.GaniAttributes[0] = "attr-one";
        account.GaniAttributes[29] = "attr-thirty";
        account.Chests.Add("level.nw:10,20");
        account.Weapons.Add("sword");
        account.Flags["client.flag"] = "value";
        account.Flags["empty.flag"] = string.Empty;
        account.FolderRights.Add("rw levels/*");
        return account;
    }

    private static string ExpectedPopulatedAccountText() =>
        string.Join(
            "\r\n",
            "GRACC001",
            "NAME pc:Ruan",
            "NICK Ruan",
            "COMMUNITYNAME pc:Ruan",
            "LEVEL start.nw",
            "X 30",
            "Y 30.5",
            "Z 1.5",
            "MAXHP 5",
            "HP 4.5",
            "RUPEES 12",
            "ANI walk",
            "ARROWS 7",
            "BOMBS 8",
            "GLOVEP 2",
            "SHIELDP 3",
            "SWORDP 3",
            "BOWP 2",
            "BOW bow2.png",
            "HEAD head1.png",
            "BODY body2.png",
            "SWORD sword3.png",
            "SHIELD shield3.png",
            "COLORS 1,2,3,4,5",
            "SPRITE 4",
            "STATUS 1",
            "MP 6",
            "AP 77",
            "APCOUNTER 2",
            "ONSECS 9",
            "IP 123",
            "LANGUAGE Portuguese",
            "KILLS 10",
            "DEATHS 11",
            "RATING 1600",
            "DEVIATION 320",
            "LASTSPARTIME 42",
            "ATTR1 attr-one",
            "ATTR30 attr-thirty",
            "CHEST level.nw:10,20",
            "WEAPON sword",
            "FLAG client.flag=value",
            "FLAG empty.flag",
            string.Empty,
            "BANNED 1",
            "BANREASON reason",
            "BANLENGTH forever",
            "COMMENTS comment",
            "EMAIL ruan@example.test",
            "LOCALRIGHTS 16384",
            "IPRANGE 127.0.0.*",
            "LOADONLY 0",
            "FOLDERRIGHT rw levels/*",
            "LASTFOLDER levels/",
            string.Empty);

    private sealed class MemoryPersistenceFileSystem(string serverPath) : IAccountPersistenceFileSystem
    {
        public string? ExistingName { get; set; }
        public bool WriteResult { get; set; } = true;
        public string? WrittenPath { get; private set; }
        public string? WrittenContents { get; private set; }
        public string? AddedFile { get; private set; }
        public string ServerPath { get; } = serverPath;

        public string? FindCaseInsensitive(string fileName) => null;

        public string? ReadAllText(string path) => null;

        public string? FileExistsAs(string fileName) => ExistingName;

        public bool WriteAllText(string path, string contents)
        {
            WrittenPath = path;
            WrittenContents = contents;
            return WriteResult;
        }

        public void AddFile(string relativePath) =>
            AddedFile = relativePath;
    }
}

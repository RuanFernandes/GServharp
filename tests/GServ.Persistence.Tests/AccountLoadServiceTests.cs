using GServ.Persistence;
using Xunit;

namespace GServ.Persistence.Tests;

public sealed class AccountLoadServiceTests
{
    [Fact]
    public void LoadUsesCaseInsensitiveAccountFilesystemLookupBeforeDefaultAccount()
    {
        var filesystem = new MemoryAccountFileSystem(@"C:\gserver\");
        filesystem.AddExisting(@"C:\gserver\accounts\PC-Ruan.TXT", "PC-Ruan.TXT", "GRACC001\nNICK Ruan\nLEVEL existing.nw");

        var result = AccountLoadService.Load("pc-ruan", filesystem, AccountLoadSettings.Empty);

        Assert.True(result.Success);
        Assert.False(result.LoadedFromDefault);
        Assert.Equal(@"C:\gserver\accounts\PC-Ruan.TXT", result.SourcePath);
        Assert.Equal("pc-ruan", result.Account!.AccountName);
        Assert.Equal("pc-ruan", result.Account.CommunityName);
        Assert.Equal("Ruan", result.Account.Nickname);
        Assert.False(result.ShouldSaveCreatedAccount);
        Assert.Null(result.AccountFileToAdd);
    }

    [Fact]
    public void LoadFallsBackToDefaultAccountAndAppliesConfiguredStartOverrides()
    {
        var filesystem = new MemoryAccountFileSystem(@"C:\gserver\");
        filesystem.AddReadable(@"C:\gserver\accounts\defaultaccount.txt", "GRACC001\nLEVEL ignored.nw\nX 1\nY 2\nLOADONLY 0");
        var settings = new AccountLoadSettings(new Dictionary<string, string>
        {
            ["startlevel"] = "onlinestartlocal.nw",
            ["startx"] = "30",
            ["starty"] = "30.5",
        });

        var result = AccountLoadService.Load("NewAccount", filesystem, settings);

        Assert.True(result.Success);
        Assert.True(result.LoadedFromDefault);
        Assert.Equal(@"C:\gserver\accounts\defaultaccount.txt", result.SourcePath);
        Assert.Equal("onlinestartlocal.nw", result.Account!.LevelName);
        Assert.Equal(480, result.Account.PixelX);
        Assert.Equal(488, result.Account.PixelY);
        Assert.True(result.ShouldSaveCreatedAccount);
        Assert.Equal(@"accounts/NewAccount.txt", result.AccountFileToAdd);
    }

    [Fact]
    public void LoadDoesNotRequestDefaultAccountSaveWhenLoadedAccountIsLoadOnly()
    {
        var filesystem = new MemoryAccountFileSystem(@"C:\gserver\");
        filesystem.AddReadable(@"C:\gserver\accounts\defaultaccount.txt", "GRACC001\nLOADONLY 1");

        var result = AccountLoadService.Load("ReadOnlyAccount", filesystem, AccountLoadSettings.Empty);

        Assert.True(result.Success);
        Assert.True(result.LoadedFromDefault);
        Assert.True(result.Account!.IsLoadOnly);
        Assert.False(result.ShouldSaveCreatedAccount);
        Assert.Null(result.AccountFileToAdd);
    }

    [Fact]
    public void LoadRejectsMissingOrMalformedResolvedFileWithoutSideEffects()
    {
        var filesystem = new MemoryAccountFileSystem(@"C:\gserver\");
        filesystem.AddReadable(@"C:\gserver\accounts\defaultaccount.txt", "GRACC000\n");

        var result = AccountLoadService.Load("MissingAccount", filesystem, AccountLoadSettings.Empty);

        Assert.False(result.Success);
        Assert.Null(result.Account);
        Assert.True(result.LoadedFromDefault);
        Assert.False(result.ShouldSaveCreatedAccount);
        Assert.Null(result.AccountFileToAdd);
    }

    [Fact]
    public void LoadGuestMarksSourceConfirmedLoadOnlyAndLeavesIdentityGenerationBlocked()
    {
        var filesystem = new MemoryAccountFileSystem(@"C:\gserver\");
        filesystem.AddExisting(@"C:\gserver\accounts\guest.txt", "guest.txt", "GRACC001\nLOADONLY 0");

        var result = AccountLoadService.Load("guest", filesystem, AccountLoadSettings.Empty);

        Assert.True(result.Success);
        Assert.True(result.Account!.IsLoadOnly);
        Assert.True(result.RequiresGuestIdentityGeneration);
        Assert.Equal("guest", result.Account.CommunityName);
        Assert.False(result.ShouldSaveCreatedAccount);
    }

    private sealed class MemoryAccountFileSystem(string serverPath) : IAccountFileSystem
    {
        private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _findi = new(StringComparer.OrdinalIgnoreCase);

        public string ServerPath { get; } = serverPath;

        public string? FindCaseInsensitive(string fileName) =>
            _findi.TryGetValue(fileName, out var path) ? path : null;

        public string? ReadAllText(string path) =>
            _files.TryGetValue(path, out var contents) ? contents : null;

        public void AddExisting(string path, string indexedFileName, string contents)
        {
            AddReadable(path, contents);
            _findi[indexedFileName] = path;
        }

        public void AddReadable(string path, string contents) =>
            _files[path] = contents;
    }
}

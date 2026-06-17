using GServ.Game;

namespace GServ.Game.Tests;

public sealed class NwLevelFileLoaderTests
{
    [Fact]
    public void TryLoadReturnsMissingWhenIndexedFilesystemCannotFindExactName()
    {
        using var temp = new TemporaryDirectory();
        var fileSystem = new IndexedServerFileSystem(temp.Path);
        var loader = new NwLevelFileLoader(fileSystem);

        var result = loader.TryLoad("start.nw");

        Assert.False(result.Success);
        Assert.Equal(LevelLoadStatus.Missing, result.Status);
    }

    [Fact]
    public void TryLoadParsesIndexedNwFileAndPreservesModTime()
    {
        using var temp = new TemporaryDirectory();
        var world = Directory.CreateDirectory(Path.Combine(temp.Path, "world"));
        var levelPath = Path.Combine(world.FullName, "start.nw");
        File.WriteAllText(
            levelPath,
            """
            GLEVNW01
            BOARD 0 0 1 0 AB
            LINK next.nw 1 2 3 4 5 6
            SIGN 4 5
            A
            SIGNEND
            CHEST 10 11 redrupee 3
            """);
        var modTime = new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
        File.SetLastWriteTimeUtc(levelPath, modTime.UtcDateTime);

        var fileSystem = new IndexedServerFileSystem(temp.Path);
        fileSystem.AddDirectory("world", "*.nw");
        var loader = new NwLevelFileLoader(fileSystem);

        var result = loader.TryLoad("start.nw", linkTargetExists: levelName => levelName == "next.nw");

        Assert.True(result.Success);
        Assert.Equal(LevelLoadStatus.Loaded, result.Status);
        Assert.Equal("start.nw", result.LevelName);
        Assert.Equal(modTime.ToUnixTimeSeconds(), result.ModTime);
        Assert.Single(result.Level.Links);
        Assert.Single(result.Level.Signs);
        Assert.Single(result.Level.Chests);
    }

    [Fact]
    public void TryLoadRejectsKnownButUnsupportedLevelFormats()
    {
        using var temp = new TemporaryDirectory();
        var world = Directory.CreateDirectory(Path.Combine(temp.Path, "world"));
        File.WriteAllText(Path.Combine(world.FullName, "start.graal"), "GR-V1.03");

        var fileSystem = new IndexedServerFileSystem(temp.Path);
        fileSystem.AddDirectory("world", "*");
        var loader = new NwLevelFileLoader(fileSystem);

        var result = loader.TryLoad("start.graal");

        Assert.False(result.Success);
        Assert.Equal(LevelLoadStatus.UnsupportedFormat, result.Status);
    }

    [Fact]
    public void ToModernLevelPayloadUsesSourceConfirmedStaticNwPacketBuilders()
    {
        using var temp = new TemporaryDirectory();
        var world = Directory.CreateDirectory(Path.Combine(temp.Path, "world"));
        var levelPath = Path.Combine(world.FullName, "start.nw");
        File.WriteAllText(
            levelPath,
            """
            GLEVNW01
            BOARD 0 0 1 0 AB
            LINK next.nw 1 2 3 4 5 6
            SIGN 4 5
            A
            SIGNEND
            CHEST 10 11 redrupee 3
            """);

        var fileSystem = new IndexedServerFileSystem(temp.Path);
        fileSystem.AddDirectory("world", "*.nw");
        var loader = new NwLevelFileLoader(fileSystem);
        var loaded = loader.TryLoad("start.nw", linkTargetExists: levelName => levelName == "next.nw");

        var payload = loaded.ToModernStaticPayload(playerHasChest: _ => false);

        Assert.Equal("start.nw", payload.LevelName);
        Assert.Equal([33, (byte)'n', (byte)'e', (byte)'x', (byte)'t', (byte)'.', (byte)'n', (byte)'w',
            (byte)' ', (byte)'1', (byte)' ', (byte)'2', (byte)' ', (byte)'3', (byte)' ', (byte)'4',
            (byte)' ', (byte)'5', (byte)' ', (byte)'6', 10], payload.LinksPacket);
        Assert.Equal([37, 36, 37, 32, 128, 10], payload.SignsPacket);
        Assert.Equal([36, 32, 42, 43, 34, 35, 10], payload.ChestsPacket);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}

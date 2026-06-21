using Preagonal.GServer.Game;

namespace Preagonal.GServer.Game.Tests;

public sealed class ServerFileSystemTests
{
    [Fact]
    public void AddDirectoryIndexesMatchingFilesByFilenameForExactFind()
    {
        using var temp = new TemporaryDirectory();
        var world = Directory.CreateDirectory(Path.Combine(temp.Path, "world"));
        var levelPath = Path.Combine(world.FullName, "start.nw");
        File.WriteAllText(levelPath, "GLEVNW01");
        File.WriteAllText(Path.Combine(world.FullName, "readme.txt"), "ignored");

        var fileSystem = new IndexedServerFileSystem(temp.Path);
        fileSystem.AddDirectory("world", "*.nw");

        Assert.Equal(levelPath, fileSystem.Find("start.nw"));
        Assert.Equal(string.Empty, fileSystem.Find("START.NW"));
        Assert.Equal(string.Empty, fileSystem.Find("readme.txt"));
    }

    [Fact]
    public void AddDirectoryUsesRecursiveTraversalWhenRequested()
    {
        using var temp = new TemporaryDirectory();
        var nested = Directory.CreateDirectory(Path.Combine(temp.Path, "world", "inside"));
        var levelPath = Path.Combine(nested.FullName, "start.nw");
        File.WriteAllText(levelPath, "GLEVNW01");

        var fileSystem = new IndexedServerFileSystem(temp.Path);
        fileSystem.AddDirectory("world", "*.nw", forceRecursive: true);

        Assert.Equal(levelPath, fileSystem.Find("start.nw"));
    }

    [Fact]
    public void RecursiveChildDirectoriesUseWildcardStarLikeCppAddDir()
    {
        using var temp = new TemporaryDirectory();
        var nested = Directory.CreateDirectory(Path.Combine(temp.Path, "world", "inside"));
        var textPath = Path.Combine(nested.FullName, "notes.txt");
        File.WriteAllText(textPath, "indexed by recursive child addDir wildcard");

        var fileSystem = new IndexedServerFileSystem(temp.Path);
        fileSystem.AddDirectory("world", "*.nw", forceRecursive: true);

        Assert.Equal(textPath, fileSystem.Find("notes.txt"));
    }

    [Fact]
    public void LoadAndGetModTimeReturnEmptyAndZeroWhenFindFails()
    {
        using var temp = new TemporaryDirectory();
        var fileSystem = new IndexedServerFileSystem(temp.Path);

        Assert.Equal(string.Empty, fileSystem.Load("missing.nw"));
        Assert.Equal(0, fileSystem.GetModTime("missing.nw"));
    }

    [Fact]
    public void ResyncPreservesRecursiveDirectories()
    {
        using var temp = new TemporaryDirectory();
        var nested = Directory.CreateDirectory(Path.Combine(temp.Path, "world", "inside"));
        var originalPath = Path.Combine(nested.FullName, "start.nw");
        File.WriteAllText(originalPath, "GLEVNW01");

        var fileSystem = new IndexedServerFileSystem(temp.Path);
        fileSystem.AddDirectory("world", "*.nw", forceRecursive: true);
        Assert.Equal(originalPath, fileSystem.Find("start.nw"));

        File.Delete(originalPath);
        var newPath = Path.Combine(nested.FullName, "next.nw");
        File.WriteAllText(newPath, "GLEVNW01");

        fileSystem.Resync();

        Assert.Equal(string.Empty, fileSystem.Find("start.nw"));
        Assert.Equal(newPath, fileSystem.Find("next.nw"));
    }

    [Fact]
    public void LoadFolderConfigParsesTypedEntriesAndMirrorsThemToAllFilesystem()
    {
        using var temp = new TemporaryDirectory();
        var levels = Directory.CreateDirectory(Path.Combine(temp.Path, "world", "levels"));
        var images = Directory.CreateDirectory(Path.Combine(temp.Path, "world", "images"));
        var levelPath = Path.Combine(levels.FullName, "start.nw");
        var imagePath = Path.Combine(images.FullName, "logo.png");
        File.WriteAllText(levelPath, "GLEVNW01");
        File.WriteAllText(imagePath, "png");

        var fileSystems = ServerResourceFileSystems.LoadFolderConfig(
            temp.Path,
            """
            # ignored
            level levels/*.nw # trailing comment
            file images/*.png
            unknown levels/*.txt
            """);

        Assert.Equal(levelPath, fileSystems.Get(ServerFileSystemKind.Level).Find("start.nw"));
        Assert.Equal(string.Empty, fileSystems.Get(ServerFileSystemKind.File).Find("start.nw"));
        Assert.Equal(imagePath, fileSystems.Get(ServerFileSystemKind.File).Find("logo.png"));
        Assert.Equal(levelPath, fileSystems.Get(ServerFileSystemKind.All).Find("start.nw"));
        Assert.Equal(imagePath, fileSystems.Get(ServerFileSystemKind.All).Find("logo.png"));
    }

    [Fact]
    public void LoadFolderConfigComparesFilesystemTypesCaseInsensitively()
    {
        using var temp = new TemporaryDirectory();
        var levels = Directory.CreateDirectory(Path.Combine(temp.Path, "world", "levels"));
        var levelPath = Path.Combine(levels.FullName, "start.nw");
        File.WriteAllText(levelPath, "GLEVNW01");

        var fileSystems = ServerResourceFileSystems.LoadFolderConfig(temp.Path, "LEVEL levels/*.nw");

        Assert.Equal(levelPath, fileSystems.Get(ServerFileSystemKind.Level).Find("start.nw"));
    }

    [Fact]
    public void LoadAllFoldersIndexesWorldAndCommaSeparatedShareFoldersIntoAllOnly()
    {
        using var temp = new TemporaryDirectory();
        var world = Directory.CreateDirectory(Path.Combine(temp.Path, "world"));
        var shared = Directory.CreateDirectory(Path.Combine(temp.Path, "shared"));
        var worldLevel = Path.Combine(world.FullName, "start.nw");
        var sharedFile = Path.Combine(shared.FullName, "shared.txt");
        File.WriteAllText(worldLevel, "GLEVNW01");
        File.WriteAllText(sharedFile, "share");

        var fileSystems = ServerResourceFileSystems.LoadAllFolders(temp.Path, " shared ");

        Assert.Equal(worldLevel, fileSystems.Get(ServerFileSystemKind.All).Find("start.nw"));
        Assert.Equal(sharedFile, fileSystems.Get(ServerFileSystemKind.All).Find("shared.txt"));
        Assert.Equal(string.Empty, fileSystems.Get(ServerFileSystemKind.Level).Find("start.nw"));
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

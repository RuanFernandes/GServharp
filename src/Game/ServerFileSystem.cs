using System.Text.RegularExpressions;

namespace Preagonal.GServer.Game;

public interface IServerFileSystem
{
    string Find(string file);
    string Load(string file);
    long GetModTime(string file);
}

public enum ServerFileSystemKind
{
    All = 0,
    File = 1,
    Level = 2,
    Head = 3,
    Body = 4,
    Sword = 5,
    Shield = 6,
}

public sealed class IndexedServerFileSystem : IServerFileSystem
{
    private readonly string _serverPath;
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);
    private readonly List<(string Directory, string Wildcard, bool Recursive)> _directories = [];

    public IndexedServerFileSystem(string serverPath)
    {
        _serverPath = Path.GetFullPath(serverPath);
    }

    public void Clear()
    {
        _files.Clear();
        _directories.Clear();
    }

    public void AddDirectory(string directory, string wildcard = "*", bool forceRecursive = false, bool noFoldersConfig = false)
    {
        var normalizedDirectory = FixPathSeparators(directory);
        var fullDirectory = Path.GetFullPath(Path.Combine(_serverPath, normalizedDirectory));

        var recursive = forceRecursive || noFoldersConfig;
        _directories.Add((fullDirectory, wildcard, recursive));
        LoadAllDirectories(fullDirectory, wildcard, recursive);
    }

    public string Find(string file) =>
        _files.TryGetValue(file, out var path) ? path : string.Empty;

    public string FindInsensitive(string file)
    {
        foreach (var entry in _files)
        {
            if (string.Equals(entry.Key, file, StringComparison.OrdinalIgnoreCase))
                return entry.Value;
        }

        return string.Empty;
    }

    public string FileExistsAs(string file)
    {
        foreach (var entry in _files)
        {
            if (string.Equals(entry.Key, file, StringComparison.OrdinalIgnoreCase))
                return entry.Key;
        }

        return string.Empty;
    }

    public string Load(string file)
    {
        var path = Find(file);
        return path.Length == 0 ? string.Empty : File.ReadAllText(path);
    }

    public long GetModTime(string file)
    {
        var path = Find(file);
        return path.Length == 0 ? 0 : new DateTimeOffset(File.GetLastWriteTimeUtc(path)).ToUnixTimeSeconds();
    }

    public int GetFileSize(string file)
    {
        var path = Find(file);
        return path.Length == 0 ? 0 : checked((int)new FileInfo(path).Length);
    }

    public void Resync()
    {
        var directories = _directories.ToArray();
        _files.Clear();

        foreach (var (directory, wildcard, recursive) in directories)
            LoadAllDirectories(directory, wildcard, recursive);
    }

    private void LoadAllDirectories(string directory, string wildcard, bool recursive)
    {
        if (!Directory.Exists(directory))
            return;

        foreach (var file in Directory.EnumerateFiles(directory))
        {
            var filename = Path.GetFileName(file);
            if (MatchesWildcard(filename, wildcard))
                _files[filename] = file;
        }

        if (!recursive)
            return;

        foreach (var child in Directory.EnumerateDirectories(directory))
            LoadAllDirectories(child, "*", recursive: true);
    }

    private static string FixPathSeparators(string path) =>
        path.Replace(Path.DirectorySeparatorChar == '\\' ? '/' : '\\', Path.DirectorySeparatorChar);

    private static bool MatchesWildcard(string filename, string wildcard)
    {
        if (wildcard == "*")
            return true;

        var pattern = "^" + Regex.Escape(wildcard).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return Regex.IsMatch(filename, pattern, RegexOptions.CultureInvariant);
    }
}

public sealed class ServerResourceFileSystems
{
    private readonly Dictionary<ServerFileSystemKind, IndexedServerFileSystem> _fileSystems;

    private ServerResourceFileSystems(string serverPath)
    {
        _fileSystems = Enum.GetValues<ServerFileSystemKind>()
            .ToDictionary(kind => kind, _ => new IndexedServerFileSystem(serverPath));
    }

    public IndexedServerFileSystem Get(ServerFileSystemKind kind) =>
        _fileSystems[kind];

    public static ServerResourceFileSystems LoadAllFolders(string serverPath, string shareFolder)
    {
        var fileSystems = new ServerResourceFileSystems(serverPath);
        var all = fileSystems.Get(ServerFileSystemKind.All);
        all.AddDirectory("world", noFoldersConfig: true);

        if (shareFolder.Length > 0)
        {
            foreach (var folder in shareFolder.Split(','))
                all.AddDirectory(folder.Trim(), noFoldersConfig: true);
        }

        return fileSystems;
    }

    public static ServerResourceFileSystems LoadFolderConfig(string serverPath, string foldersConfigText)
    {
        var fileSystems = new ServerResourceFileSystems(serverPath);

        foreach (var rawLine in foldersConfigText.Split('\n'))
        {
            var line = StripComment(rawLine).Trim();
            if (line.Length == 0)
                continue;

            var separator = line.IndexOf(' ');
            var type = separator == -1 ? line : line[..separator].Trim();
            var config = separator == -1 ? string.Empty : line[(separator + 1)..].Trim();
            config = FixConfigPathSeparators(config);

            var (directory, wildcard) = SplitFolderConfig(config);
            var kind = TryGetFileSystemKind(type);

            if (kind is not null)
                fileSystems.Get(kind.Value).AddDirectory(directory, wildcard);

            fileSystems.Get(ServerFileSystemKind.All).AddDirectory(directory, wildcard);
        }

        return fileSystems;
    }

    private static string StripComment(string line)
    {
        var comment = line.IndexOf('#');
        return comment == -1 ? line : line[..comment];
    }

    private static (string Directory, string Wildcard) SplitFolderConfig(string config)
    {
        var separator = config.LastIndexOf(Path.DirectorySeparatorChar);
        if (separator == -1)
            return ("world", config);

        var directoryWithoutWildcard = config[..(separator + 1)];
        var wildcard = config[directoryWithoutWildcard.Length..];
        return ("world/" + directoryWithoutWildcard, wildcard);
    }

    private static ServerFileSystemKind? TryGetFileSystemKind(string type)
    {
        foreach (var kind in Enum.GetValues<ServerFileSystemKind>())
        {
            if (string.Equals(type, ToCppFilesystemType(kind), StringComparison.OrdinalIgnoreCase))
                return kind;
        }

        return null;
    }

    private static string ToCppFilesystemType(ServerFileSystemKind kind) =>
        kind switch
        {
            ServerFileSystemKind.All => "all",
            ServerFileSystemKind.File => "file",
            ServerFileSystemKind.Level => "level",
            ServerFileSystemKind.Head => "head",
            ServerFileSystemKind.Body => "body",
            ServerFileSystemKind.Sword => "sword",
            ServerFileSystemKind.Shield => "shield",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

    private static string FixConfigPathSeparators(string path) =>
        path.Replace(Path.DirectorySeparatorChar == '\\' ? '/' : '\\', Path.DirectorySeparatorChar);
}

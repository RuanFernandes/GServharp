namespace Preagonal.GServer.Persistence;

public sealed class DiskAccountFileSystem(string serverPath) : IAccountPersistenceFileSystem
{
    public string ServerPath { get; } = Path.GetFullPath(serverPath);

    public string? FindCaseInsensitive(string fileName)
    {
        var accountsPath = Path.Combine(ServerPath, "accounts");
        if (!Directory.Exists(accountsPath))
            return null;

        var safeFileName = SafeAccountFileName(fileName);
        foreach (var file in Directory.EnumerateFiles(accountsPath, "*.txt"))
        {
            var existing = Path.GetFileName(file);
            if (string.Equals(existing, fileName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(existing, safeFileName, StringComparison.OrdinalIgnoreCase))
                return file;
        }

        return null;
    }

    public string? ReadAllText(string path) =>
        File.Exists(path) ? File.ReadAllText(path) : null;

    public string? FileExistsAs(string fileName)
    {
        var path = FindCaseInsensitive(fileName);
        return path is null ? null : Path.GetFileName(path);
    }

    public bool WriteAllText(string path, string contents)
    {
        var safePath = SafeAccountPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(safePath)!);
        File.WriteAllText(safePath, contents);
        return true;
    }

    public void AddFile(string relativePath)
    {
    }

    private static string SafeAccountPath(string path)
    {
        var directory = Path.GetDirectoryName(path);
        var fileName = Path.GetFileName(path);
        return directory is null
            ? SafeAccountFileName(fileName)
            : Path.Combine(directory, SafeAccountFileName(fileName));
    }

    private static string SafeAccountFileName(string fileName) =>
        string.Concat(fileName.Select(ch => ch == ':' ? '_' : ch));
}

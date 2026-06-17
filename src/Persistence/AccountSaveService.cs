namespace GServ.Persistence;

public interface IAccountPersistenceFileSystem : IAccountFileSystem
{
    string? FileExistsAs(string fileName);
    bool WriteAllText(string path, string contents);
    void AddFile(string relativePath);
}

public sealed record AccountSaveResult(
    bool CppReturnValue,
    bool WriteAttempted,
    bool WriteSucceeded,
    string? Path,
    string? Contents,
    string? AccountFileAdded);

public static class AccountSaveService
{
    public static AccountSaveResult Save(AccountFileData account, IAccountPersistenceFileSystem fileSystem)
    {
        var serialized = AccountFileSerializer.Serialize(account);
        if (!serialized.Success)
            return new AccountSaveResult(false, false, false, null, null, null);

        var requestedFileName = account.AccountName + ".txt";
        var accountFileName = fileSystem.FileExistsAs(requestedFileName) ?? requestedFileName;
        var path = BuildAccountPath(fileSystem.ServerPath, accountFileName);
        var writeSucceeded = fileSystem.WriteAllText(path, serialized.Contents);

        return new AccountSaveResult(true, true, writeSucceeded, path, serialized.Contents, null);
    }

    public static AccountSaveResult SaveCreatedDefaultAccount(
        AccountFileData account,
        IAccountPersistenceFileSystem fileSystem,
        string accountName)
    {
        var saveResult = Save(account, fileSystem);
        if (!saveResult.CppReturnValue)
            return saveResult;

        var relativePath = $"accounts/{accountName}.txt";
        fileSystem.AddFile(relativePath);
        return saveResult with { AccountFileAdded = relativePath };
    }

    private static string BuildAccountPath(string serverPath, string accountFileName)
    {
        var trimmed = serverPath.TrimEnd('/', '\\');
        return string.Join(Path.DirectorySeparatorChar, trimmed, "accounts", accountFileName);
    }
}

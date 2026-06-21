namespace Preagonal.GServer.Admin;

public sealed record RcFolderRightEntry(string Rights, string Folder, string Wildcard);

public static class RcFolderRights
{
    public static RcFolderRightEntry ParseLine(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0)
        {
            return new RcFolderRightEntry("r", "*", "*");
        }

        var splitAt = trimmed.IndexOf(' ');
        var rights = splitAt < 0 ? trimmed : trimmed[..splitAt];
        var folder = splitAt < 0 ? "*" : trimmed[(splitAt + 1)..].Trim();

        rights = rights.Trim().ToLowerInvariant();
        folder = folder.Replace('\\', '/');

        var wildcard = "*";
        if (!folder.EndsWith("/", StringComparison.Ordinal))
        {
            var lastSlash = folder.LastIndexOf('/');
            if (lastSlash >= 0)
            {
                wildcard = folder[(lastSlash + 1)..];
                folder = folder[..(lastSlash + 1)];
            }
        }

        return new RcFolderRightEntry(rights, folder, wildcard);
    }
}

namespace Preagonal.GServer.Protocol;

public static class AllowedVersionPolicy
{
    public static bool IsAllowed(ClientVersionId versionId, IReadOnlyList<string> allowedVersions)
    {
        var allowed = false;
        foreach (var raw in allowedVersions)
        {
            var version = raw.Trim();
            if (version.Length == 0) continue;

            var separator = version.IndexOf(':', StringComparison.Ordinal);
            if (separator == -1)
            {
                if (versionId == GraalVersionCatalog.GetClientVersionId(version))
                {
                    allowed = true;
                    break;
                }
            }
            else
            {
                var start = GraalVersionCatalog.GetClientVersionId(version[..separator].Trim());
                var end = GraalVersionCatalog.GetClientVersionId(version[(separator + 1)..].Trim());
                if (start != ClientVersionId.Unknown &&
                    end != ClientVersionId.Unknown &&
                    versionId >= start &&
                    versionId <= end)
                {
                    allowed = true;
                    break;
                }
            }
        }

        return allowed;
    }
}

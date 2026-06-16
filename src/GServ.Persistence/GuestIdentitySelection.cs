using System.Globalization;

namespace GServ.Persistence;

public sealed record GuestIdentitySelectionResult(bool Success, string? AccountName)
{
    public static GuestIdentitySelectionResult Blocked { get; } = new(false, null);
}

public interface IGuestIdentitySelector
{
    GuestIdentitySelectionResult TrySelect(Func<string, bool> activeAccountExists);
}

public sealed class CandidateGuestIdentitySelector(IEnumerable<int> candidates) : IGuestIdentitySelector
{
    public GuestIdentitySelectionResult TrySelect(Func<string, bool> activeAccountExists)
    {
        foreach (var candidate in candidates)
        {
            var accountName = BuildAccountName(candidate);
            if (!activeAccountExists(accountName))
                return new GuestIdentitySelectionResult(true, accountName);
        }

        return GuestIdentitySelectionResult.Blocked;
    }

    private static string BuildAccountName(int candidate)
    {
        var text = candidate.ToString(CultureInfo.InvariantCulture);
        var length = Math.Min(text.Length, 6);
        return "pc:" + text[..length];
    }
}

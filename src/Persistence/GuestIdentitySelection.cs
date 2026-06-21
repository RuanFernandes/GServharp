using System.Globalization;

namespace Preagonal.GServer.Persistence;

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

public sealed class RandomGuestIdentitySelector(Random random, int maxAttempts = 100) : IGuestIdentitySelector
{
    public GuestIdentitySelectionResult TrySelect(Func<string, bool> activeAccountExists)
    {
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var accountName = "pc:" + random.Next(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
            if (!activeAccountExists(accountName))
                return new GuestIdentitySelectionResult(true, accountName);
        }

        return GuestIdentitySelectionResult.Blocked;
    }
}

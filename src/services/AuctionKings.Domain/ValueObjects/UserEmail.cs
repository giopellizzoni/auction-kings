namespace AuctionKings.Domain.ValueObjects;

public static class UserEmail
{
    /// <summary>
    /// The canonical form stored and looked up: trimmed and lower-cased, so <c>Ann@Example.com</c> and
    /// <c>ann@example.com</c> are the same account.
    /// </summary>
    public static string Normalize(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();
}

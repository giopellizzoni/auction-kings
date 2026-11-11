namespace AuctionKings.Application.Abstractions;

public sealed record RateLimitResult(bool IsAllowed, int CurrentCount, bool IsError = false);

public interface IRateLimiter
{
    /// <summary>Checks <paramref name="key"/> against the global <c>RateLimit</c> limit and window.</summary>
    Task<RateLimitResult> IsAllowedAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Checks <paramref name="key"/> against a caller-specific limit and sliding window.</summary>
    Task<RateLimitResult> IsAllowedAsync(
        string key,
        int limit,
        TimeSpan window,
        CancellationToken cancellationToken = default
    );
}

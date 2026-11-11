namespace AuctionKings.Application.Abstractions;

public sealed record RateLimitResult(bool IsAllowed, int CurrentCount, bool IsError = false);

public interface IRateLimiter
{
    Task<RateLimitResult> IsAllowedAsync(string key, CancellationToken cancellationToken = default);
}

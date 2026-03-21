namespace AuctionKings.Application.Abstractions;

public interface IRateLimiter
{
    Task<bool> IsAllowedAsync(string key, CancellationToken cancellationToken = default);
}

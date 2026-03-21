namespace AuctionKings.Infrastructure.RateLimiting;

internal sealed class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    public int MaxRequests { get; init; } = 10;
    public TimeSpan Window { get; init; } = TimeSpan.FromSeconds(10);
}

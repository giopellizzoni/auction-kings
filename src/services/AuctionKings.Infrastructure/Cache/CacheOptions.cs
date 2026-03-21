namespace AuctionKings.Infrastructure.Cache;

internal sealed class CacheOptions
{
    public const string SectionName = "Cache";

    public TimeSpan ListingsTtl { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan DetailsTtl { get; init; } = TimeSpan.FromSeconds(60);
    public TimeSpan CountersTtl { get; init; } = TimeSpan.FromSeconds(5);
}

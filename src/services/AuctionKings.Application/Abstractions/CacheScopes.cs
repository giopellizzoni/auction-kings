namespace AuctionKings.Application.Abstractions;

/// <summary>
/// The invalidation groups used with <see cref="ICacheService"/>. Entries in a scope are cached and
/// invalidated together; <see cref="ICacheInvalidationService"/> invalidates exactly these scopes.
/// </summary>
public static class CacheScopes
{
    /// <summary>An auction's details and its bid pages.</summary>
    public static string Auction(Guid auctionId) => $"auction:{auctionId}";

    /// <summary>Every page of the active-auctions listing.</summary>
    public const string ActiveListings = "auctions:active";

    /// <summary>Every page of one user's bid history.</summary>
    public static string UserBids(Guid userId) => $"user:{userId}:bids";
}

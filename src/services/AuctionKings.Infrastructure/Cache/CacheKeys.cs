namespace AuctionKings.Infrastructure.Cache;

internal static class CacheKeys
{
    public static string AuctionDetail(Guid id) => $"auction:{id}";

    public static string AuctionBidCount(Guid id) => $"auction:{id}:bidcount";

    public static string ActiveListingsPage(int page, int pageSize) =>
        $"auctions:active:page:{page}:size:{pageSize}";

    public static string AuctionPrefix(Guid id) => $"auction:{id}";

    public static string ActiveListingsPrefix() => "auctions:active:";

    public static string UserBidsPrefix(Guid userId) => $"user:{userId}:bids:";
}

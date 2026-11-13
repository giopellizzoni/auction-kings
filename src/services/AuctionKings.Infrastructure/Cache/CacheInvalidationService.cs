using AuctionKings.Application.Abstractions;

namespace AuctionKings.Infrastructure.Cache;

internal sealed class CacheInvalidationService : ICacheInvalidationService
{
    private readonly ICacheService _cacheService;

    public CacheInvalidationService(ICacheService cacheService) =>
        _cacheService = cacheService;

    public Task InvalidateAuctionAsync(
        Guid auctionId,
        CancellationToken cancellationToken = default
    ) => _cacheService.InvalidateScopeAsync(CacheScopes.Auction(auctionId), cancellationToken);

    public Task InvalidateActiveListingsAsync(
        CancellationToken cancellationToken = default
    ) => _cacheService.InvalidateScopeAsync(CacheScopes.ActiveListings, cancellationToken);

    public Task InvalidateUserBidsAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    ) => _cacheService.InvalidateScopeAsync(CacheScopes.UserBids(userId), cancellationToken);
}

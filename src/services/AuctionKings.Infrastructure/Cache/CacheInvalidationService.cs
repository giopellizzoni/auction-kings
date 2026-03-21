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
    ) => _cacheService.RemoveByPrefixAsync(CacheKeys.AuctionPrefix(auctionId), cancellationToken);

    public Task InvalidateActiveListingsAsync(
        CancellationToken cancellationToken = default
    ) => _cacheService.RemoveByPrefixAsync(CacheKeys.ActiveListingsPrefix(), cancellationToken);
}

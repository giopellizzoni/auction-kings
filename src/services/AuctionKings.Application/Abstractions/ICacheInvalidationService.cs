namespace AuctionKings.Application.Abstractions;

public interface ICacheInvalidationService
{
    Task InvalidateAuctionAsync(Guid auctionId, CancellationToken cancellationToken = default);

    Task InvalidateActiveListingsAsync(CancellationToken cancellationToken = default);
}

using AuctionKings.Domain.Aggregates;

namespace AuctionKings.Application.Abstractions;

public interface IBidRepository
{
    Task<IReadOnlyList<Bid>> GetByAuctionIdAsync(
        Guid auctionId,
        CancellationToken cancellationToken = default
    );

    Task<(IReadOnlyList<Bid> Items, int TotalCount)> GetByAuctionIdPagedAsync(
        Guid auctionId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default
    );

    Task<(IReadOnlyList<Bid> Items, int TotalCount)> GetByBidderIdPagedAsync(
        Guid bidderId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(Bid bid, CancellationToken cancellationToken = default);
}

using AuctionKings.Domain.Aggregates;

namespace AuctionKings.Application.Abstractions;

public interface IBidRepository
{
    Task<IReadOnlyList<Bid>> GetByAuctionIdAsync(
        Guid auctionId,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(Bid bid, CancellationToken cancellationToken = default);
}

using AuctionKings.Domain.Aggregates;

namespace AuctionKings.Application.Abstractions;

public interface IAuctionRepository
{
    Task<Auction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Auction>> GetActiveAuctionsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(Auction auction, CancellationToken cancellationToken = default);

    Task UpdateAsync(Auction auction, CancellationToken cancellationToken = default);
}

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

    Task<int> CountActiveAuctionsAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Auction auction, CancellationToken cancellationToken = default);

    Task UpdateAsync(Auction auction, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Auction>> GetExpiredAuctionsAsync(
        DateTime now,
        int batchSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Auction>> GetPendingAuctionsToActivateAsync(
        DateTime now,
        int batchSize,
        CancellationToken cancellationToken = default);
}

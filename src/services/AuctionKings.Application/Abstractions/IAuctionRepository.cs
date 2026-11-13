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

    /// <summary>
    /// Pending auctions that are due: <c>StartTime &lt;= now</c> and not yet ended (<c>EndTime &gt; now</c>),
    /// oldest start first. Ended-but-never-activated auctions are excluded; they stay Pending.
    /// </summary>
    Task<IReadOnlyList<Auction>> GetPendingAuctionsToActivateAsync(
        DateTime now,
        int batchSize,
        CancellationToken cancellationToken = default);
}

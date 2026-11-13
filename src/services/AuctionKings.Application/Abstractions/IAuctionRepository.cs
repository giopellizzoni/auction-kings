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
    /// Pending auctions whose <c>StartTime &lt;= now</c>, oldest start first. This deliberately includes ones that
    /// already ended: activating them is rejected and ActivateAuctionCommandHandler cancels them, which removes
    /// them from the Pending set (so they cannot starve later auctions).
    /// </summary>
    Task<IReadOnlyList<Auction>> GetPendingAuctionsToActivateAsync(
        DateTime now,
        int batchSize,
        CancellationToken cancellationToken = default);
}

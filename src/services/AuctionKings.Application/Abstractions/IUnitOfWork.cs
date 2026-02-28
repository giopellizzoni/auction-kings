namespace AuctionKings.Application.Abstractions;

public interface IUnitOfWork
{
    /// <summary>
    /// Dispatches pending domain events to the Outbox, then commits the transaction.
    /// </summary>
    Task<int> CommitAsync(CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

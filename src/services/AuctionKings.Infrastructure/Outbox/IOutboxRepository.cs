namespace AuctionKings.Infrastructure.Outbox;

internal interface IOutboxRepository
{
    /// <summary>
    /// Runs <paramref name="work"/> inside a database transaction that is wrapped in the
    /// configured EF Core execution strategy. A retrying strategy (<c>EnableRetryOnFailure</c>)
    /// rejects user-initiated transactions unless they are opened through the strategy, and on a
    /// transient failure the whole unit (fetch + process + save) is replayed.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken);

    Task<IReadOnlyList<Persistence.OutboxMessage>> FetchPendingAsync(
        int batchSize,
        CancellationToken cancellationToken
    );

    Task SaveAsync(CancellationToken cancellationToken);
}

namespace AuctionKings.Infrastructure.Outbox;

internal interface IOutboxRepository
{
    /// <summary>
    /// Runs <paramref name="operation"/> inside one database transaction, committed when it returns,
    /// through the context's execution strategy so it works with <c>EnableRetryOnFailure</c>.
    /// On a transient failure the transaction rolls back and the whole operation runs again, so it must be
    /// safe to repeat from the start (fetch included).
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken);

    Task<IReadOnlyList<Persistence.OutboxMessage>> FetchPendingAsync(
        int batchSize,
        CancellationToken cancellationToken
    );

    Task SaveAsync(CancellationToken cancellationToken);
}

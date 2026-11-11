namespace AuctionKings.Infrastructure.Outbox;

internal interface IOutboxRepository
{
    Task BeginTransactionAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<Persistence.OutboxMessage>> FetchPendingAsync(
        int batchSize,
        CancellationToken cancellationToken
    );

    Task SaveAsync(CancellationToken cancellationToken);

    Task CommitAsync(CancellationToken cancellationToken);
}

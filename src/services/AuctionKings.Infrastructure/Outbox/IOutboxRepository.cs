namespace AuctionKings.Infrastructure.Outbox;

internal interface IOutboxRepository
{
    Task<IReadOnlyList<Persistence.OutboxMessage>> FetchPendingAsync(
        int batchSize,
        CancellationToken cancellationToken
    );

    Task SaveAsync(CancellationToken cancellationToken);
}

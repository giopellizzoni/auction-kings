using AuctionKings.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuctionKings.Infrastructure.Outbox;

internal sealed class OutboxRepository : IOutboxRepository
{
    private readonly AuctionDbContext _context;

    public OutboxRepository(AuctionDbContext context)
    {
        _context = context;
    }

    public Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> work,
        CancellationToken cancellationToken
    )
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return strategy.ExecuteAsync(async () =>
        {
            // A replayed attempt must not see entities tracked (and mutated) by the failed one.
            _context.ChangeTracker.Clear();

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            await work(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    public async Task<IReadOnlyList<OutboxMessage>> FetchPendingAsync(
        int batchSize,
        CancellationToken cancellationToken
    )
    {
        return await _context
            .OutboxMessages.FromSqlRaw(
                """
                SELECT * FROM outbox_messages
                WHERE processed_at IS NULL AND failed = false
                ORDER BY occurred_on
                LIMIT {0}
                FOR UPDATE SKIP LOCKED
                """,
                batchSize
            )
            .ToListAsync(cancellationToken);
    }

    public Task SaveAsync(CancellationToken cancellationToken)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}

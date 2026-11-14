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
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken
    )
    {
        // The context uses NpgsqlRetryingExecutionStrategy (EnableRetryOnFailure), which rejects a transaction
        // begun outside it. Run begin → operation → commit as one unit the strategy can retry.
        var strategy = _context.Database.CreateExecutionStrategy();

        return strategy.ExecuteAsync(
            async token =>
            {
                // A retry starts from scratch after a rollback: drop the entities the failed attempt tracked
                // (and modified), otherwise identity resolution hands the re-fetch those stale instances.
                _context.ChangeTracker.Clear();

                await using var transaction = await _context.Database.BeginTransactionAsync(token);
                await operation(token);
                await transaction.CommitAsync(token);
            },
            cancellationToken
        );
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

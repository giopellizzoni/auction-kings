using AuctionKings.Domain.Events;
using AuctionKings.Domain.ValueObjects;
using AuctionKings.Infrastructure.Outbox;
using AuctionKings.Infrastructure.Persistence;
using AuctionKings.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AuctionKings.IntegrationTests.Persistence;

[Collection("PostgreSql")]
public sealed class OutboxRepositoryTests
{
    // Large enough to include every pending row other tests in the collection leave behind.
    private const int BatchSize = 1000;

    private readonly PostgreSqlFixture _fixture;

    public OutboxRepositoryTests(PostgreSqlFixture fixture) => _fixture = fixture;

    private async Task<Guid> InsertPendingMessageAsync()
    {
        var domainEvent = new AuctionCreatedEvent(
            Guid.NewGuid(),
            "Outbox Test Auction",
            Guid.NewGuid(),
            new Money(100m, "USD"),
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(1)
        );
        var message = OutboxMessage.Create(domainEvent);

        await using var context = _fixture.CreateDbContext();
        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync();

        return message.Id;
    }

    [Fact]
    public async Task ExecuteInTransaction_WithRetryingExecutionStrategy_ShouldFetchAndPersistTheBatch()
    {
        // Regression: a manual BeginTransactionAsync under EnableRetryOnFailure threw
        // "NpgsqlRetryingExecutionStrategy does not support user-initiated transactions".
        var messageId = await InsertPendingMessageAsync();
        var processedAt = DateTime.UtcNow;

        await using (var context = _fixture.CreateRetryingDbContext())
        {
            var repository = new OutboxRepository(context);

            await repository.ExecuteInTransactionAsync(
                async token =>
                {
                    var messages = await repository.FetchPendingAsync(BatchSize, token);
                    Assert.Single(messages, m => m.Id == messageId).MarkAsProcessed(processedAt);
                    await repository.SaveAsync(token);
                },
                CancellationToken.None
            );
        }

        await using var readContext = _fixture.CreateDbContext();
        var stored = await readContext.OutboxMessages.SingleAsync(m => m.Id == messageId);
        Assert.NotNull(stored.ProcessedAt);
    }

    [Fact]
    public async Task ExecuteInTransaction_ShouldHoldTheRowLocksUntilCommit()
    {
        // FOR UPDATE SKIP LOCKED only keeps a second worker off the batch while the transaction is open.
        var messageId = await InsertPendingMessageAsync();

        await using var firstContext = _fixture.CreateRetryingDbContext();
        await using var secondContext = _fixture.CreateRetryingDbContext();
        var first = new OutboxRepository(firstContext);
        var second = new OutboxRepository(secondContext);

        await first.ExecuteInTransactionAsync(
            async token =>
            {
                var firstBatch = await first.FetchPendingAsync(BatchSize, token);
                Assert.Contains(firstBatch, m => m.Id == messageId);

                await second.ExecuteInTransactionAsync(
                    async innerToken =>
                    {
                        var secondBatch = await second.FetchPendingAsync(BatchSize, innerToken);
                        Assert.DoesNotContain(secondBatch, m => m.Id == messageId);
                    },
                    token
                );
            },
            CancellationToken.None
        );
    }
}

using System.Text.Json;
using AuctionKings.Domain.Aggregates;
using AuctionKings.Domain.Events;
using AuctionKings.Domain.ValueObjects;
using AuctionKings.Infrastructure.Persistence;
using AuctionKings.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AuctionKings.IntegrationTests.Persistence;

[Collection("PostgreSql")]
public sealed class UnitOfWorkTests
{
    private readonly PostgreSqlFixture _fixture;

    public UnitOfWorkTests(PostgreSqlFixture fixture) => _fixture = fixture;

    private static Auction CreateActiveAuction(Guid sellerId)
    {
        var auction = Auction.Create(
            "Test Auction",
            "Integration test auction",
            new Money(100m, "USD"),
            DateTime.UtcNow.AddMinutes(-1),
            AuctionDuration.FromDays(1),
            sellerId
        ).Value;
        auction.Activate(DateTime.UtcNow.AddMinutes(-1));
        return auction;
    }

    [Fact]
    public async Task UnitOfWork_Commit_ShouldPersistOutboxMessages()
    {
        await using var context = _fixture.CreateDbContext();
        var unitOfWork = new UnitOfWork(context);
        var sellerId = Guid.NewGuid();

        var auction = CreateActiveAuction(sellerId);
        await context.Auctions.AddAsync(auction);

        // CommitAsync should collect domain events (AuctionCreatedEvent) and write OutboxMessages
        await unitOfWork.CommitAsync();

        await using var readContext = _fixture.CreateDbContext();
        var outboxCount = await readContext.OutboxMessages.CountAsync();
        Assert.True(outboxCount > 0);
    }

    [Fact]
    public async Task UnitOfWork_Commit_ShouldClearDomainEvents()
    {
        await using var context = _fixture.CreateDbContext();
        var unitOfWork = new UnitOfWork(context);
        var sellerId = Guid.NewGuid();

        var auction = CreateActiveAuction(sellerId);
        await context.Auctions.AddAsync(auction);

        // Before commit: events should exist
        Assert.NotEmpty(auction.GetDomainEvents());

        await unitOfWork.CommitAsync();

        // After commit: events should be cleared
        Assert.Empty(auction.GetDomainEvents());
    }

    [Fact]
    public async Task UnitOfWork_Commit_CancelledAuction_ShouldPersistResolvableCancelledEventInOutbox()
    {
        await using var context = _fixture.CreateDbContext();
        var unitOfWork = new UnitOfWork(context);
        var auction = CreateActiveAuction(Guid.NewGuid());
        await context.Auctions.AddAsync(auction);
        await unitOfWork.CommitAsync();

        var cancelledAt = DateTime.UtcNow;
        Assert.True(auction.Cancel(cancelledAt).IsSuccess);
        await unitOfWork.CommitAsync();

        await using var readContext = _fixture.CreateDbContext();
        var cancelledType = typeof(AuctionCancelledEvent).FullName;
        var messages = await readContext.OutboxMessages.Where(m => m.Type == cancelledType).ToListAsync();
        var message = Assert.Single(messages, m => m.Content.Contains(auction.Id.ToString()));
        var type = DomainEventTypeRegistry.Resolve(message.Type);
        Assert.NotNull(type);
        Assert.Equal(typeof(AuctionCancelledEvent), type);
        var deserialized = Assert.IsType<AuctionCancelledEvent>(JsonSerializer.Deserialize(message.Content, type));
        Assert.Equal(auction.Id, deserialized.AuctionId);
    }
}

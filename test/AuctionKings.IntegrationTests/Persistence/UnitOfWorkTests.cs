using AuctionKings.Domain.Aggregates;
using AuctionKings.Domain.ValueObjects;
using AuctionKings.Infrastructure.Persistence;
using AuctionKings.Infrastructure.Repositories;
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
        );
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
}

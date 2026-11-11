using AuctionKings.Domain.Aggregates;
using AuctionKings.Domain.ValueObjects;
using AuctionKings.Infrastructure.Repositories;
using AuctionKings.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AuctionKings.IntegrationTests.Repositories;

[Collection("PostgreSql")]
public sealed class AuctionRepositoryTests
{
    private readonly PostgreSqlFixture _fixture;

    public AuctionRepositoryTests(PostgreSqlFixture fixture) => _fixture = fixture;

    private static Auction CreateSampleAuction()
    {
        return Auction.Create(
            "Vintage Watch",
            "A rare 1960s Swiss watch",
            new Money(500m, "USD"),
            DateTime.UtcNow.AddHours(1),
            AuctionDuration.FromDays(1),
            Guid.NewGuid()
        );
    }

    [Fact]
    public async Task AuctionRepository_AddAndRetrieve_ShouldWork()
    {
        await using var context = _fixture.CreateDbContext();
        var repository = new AuctionRepository(context);
        var auction = CreateSampleAuction();

        await repository.AddAsync(auction);
        await context.SaveChangesAsync();

        await using var readContext = _fixture.CreateDbContext();
        var readRepository = new AuctionRepository(readContext);
        var retrieved = await readRepository.GetByIdAsync(auction.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(auction.Id, retrieved.Id);
        Assert.Equal("Vintage Watch", retrieved.Title);
        Assert.Equal(500m, retrieved.CurrentPrice.Amount);
        Assert.Equal("USD", retrieved.CurrentPrice.Currency);
    }

    [Fact]
    public async Task AuctionRepository_Update_WithStaleVersion_ShouldThrowConcurrencyException()
    {
        // Arrange: persist an active auction (Close() increments Version, which is the concurrency token)
        await using var setupContext = _fixture.CreateDbContext();
        var setupRepository = new AuctionRepository(setupContext);
        var auction = Auction.Create(
            "Stale Version Test Auction",
            "Tests optimistic concurrency via Version token",
            new Money(500m, "USD"),
            DateTime.UtcNow.AddHours(-2),
            AuctionDuration.FromDays(1),
            Guid.NewGuid()
        );
        auction.Activate(DateTime.UtcNow.AddHours(-2));
        await setupRepository.AddAsync(auction);
        await setupContext.SaveChangesAsync();

        // Load and update from context A
        await using var contextA = _fixture.CreateDbContext();
        var repoA = new AuctionRepository(contextA);
        var auctionA = await repoA.GetByIdAsync(auction.Id);
        Assert.NotNull(auctionA);
        auctionA.Close(DateTime.UtcNow, force: true); // increments Version

        // Load and update from context B (simulates concurrent transaction)
        await using var contextB = _fixture.CreateDbContext();
        var repoB = new AuctionRepository(contextB);
        var auctionB = await repoB.GetByIdAsync(auction.Id);
        Assert.NotNull(auctionB);
        auctionB.Close(DateTime.UtcNow, force: true); // increments Version

        // Commit B first — B's version goes from 0→1 in DB
        await repoB.UpdateAsync(auctionB);
        await contextB.SaveChangesAsync();

        // Commit A — A still has original Version=0, but DB now has Version=1 → concurrency conflict
        await repoA.UpdateAsync(auctionA);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => contextA.SaveChangesAsync()
        );
    }
}

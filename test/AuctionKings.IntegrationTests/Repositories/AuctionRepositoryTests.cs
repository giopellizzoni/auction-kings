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
        ).Value;
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
    public async Task AuctionRepository_GetPendingAuctionsToActivate_ShouldSkipEndedAuctionsEvenWhenTheyFillTheBatch()
    {
        const int batchSize = 3;
        var now = DateTime.UtcNow;

        // Pending auctions that already ended (one ends exactly at `now`), all starting before the valid one so that
        // StartTime ordering would put them first and fill the whole batch.
        var endedIds = new List<Guid>();
        for (var i = 0; i < batchSize + 1; i++)
        {
            endedIds.Add(await AddPendingAuctionAsync(
                startTime: now.AddDays(-3).AddMinutes(i),
                endTime: i == 0 ? now : now.AddHours(-1 - i)));
        }

        var validId = await AddPendingAuctionAsync(startTime: now.AddHours(-2), endTime: now.AddHours(2));

        await using var readContext = _fixture.CreateDbContext();
        var repository = new AuctionRepository(readContext);

        var due = await repository.GetPendingAuctionsToActivateAsync(now, batchSize);

        Assert.Contains(due, a => a.Id == validId);
        Assert.DoesNotContain(due, a => endedIds.Contains(a.Id));
    }

    // Auction.Create makes a past-start auction Active, so seed it Pending with a future window and then move the
    // window into the past directly, which is the state a never-activated auction ends up in.
    private async Task<Guid> AddPendingAuctionAsync(DateTime startTime, DateTime endTime)
    {
        await using var context = _fixture.CreateDbContext();
        var auction = CreateSampleAuction();
        await new AuctionRepository(context).AddAsync(auction);
        await context.SaveChangesAsync();

        await context.Auctions
            .Where(a => a.Id == auction.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.StartTime, startTime)
                .SetProperty(a => a.EndTime, endTime));

        return auction.Id;
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
        ).Value;
        auction.Activate(DateTime.UtcNow.AddHours(-2));
        await setupRepository.AddAsync(auction);
        await setupContext.SaveChangesAsync();

        // Load and update from context A
        await using var contextA = _fixture.CreateDbContext();
        var repoA = new AuctionRepository(contextA);
        var auctionA = await repoA.GetByIdAsync(auction.Id);
        Assert.NotNull(auctionA);
        auctionA.Close(DateTime.UtcNow); // increments Version

        // Load and update from context B (simulates concurrent transaction)
        await using var contextB = _fixture.CreateDbContext();
        var repoB = new AuctionRepository(contextB);
        var auctionB = await repoB.GetByIdAsync(auction.Id);
        Assert.NotNull(auctionB);
        auctionB.Close(DateTime.UtcNow); // increments Version

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

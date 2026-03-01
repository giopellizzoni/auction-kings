using AuctionKings.Domain.Aggregates;
using AuctionKings.Domain.ValueObjects;
using AuctionKings.Infrastructure.Repositories;
using AuctionKings.IntegrationTests.Infrastructure;

namespace AuctionKings.IntegrationTests.Repositories;

[Collection("PostgreSql")]
public sealed class BidRepositoryTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public BidRepositoryTests(PostgreSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task BidRepository_GetByAuctionId_ShouldReturnOrderedBids()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();

        var sellerId = Guid.NewGuid();
        var auction = Auction.Create(
            "Art Piece",
            "Modern abstract",
            new Money(100m, "USD"),
            DateTime.UtcNow.AddMinutes(-10),
            AuctionDuration.FromDays(1),
            sellerId
        );
        auction.Activate(DateTime.UtcNow.AddMinutes(-10));

        await context.Auctions.AddAsync(auction);
        await context.SaveChangesAsync();

        var auctionRepository = new AuctionRepository(context);
        var tracked = await auctionRepository.GetByIdAsync(auction.Id);
        Assert.NotNull(tracked);

        tracked.PlaceBid(Guid.NewGuid(), new Money(120m, "USD"), DateTime.UtcNow.AddMinutes(-5));
        tracked.PlaceBid(Guid.NewGuid(), new Money(150m, "USD"), DateTime.UtcNow.AddMinutes(-2));
        await context.SaveChangesAsync();

        // Act
        await using var readContext = _fixture.CreateDbContext();
        var bidRepository = new BidRepository(readContext);
        var bids = await bidRepository.GetByAuctionIdAsync(auction.Id);

        // Assert: ordered descending by PlacedAt
        Assert.Equal(2, bids.Count);
        Assert.True(bids[0].PlacedAt >= bids[1].PlacedAt);
    }
}

using AuctionKings.Application;
using AuctionKings.Application.Abstractions;
using AuctionKings.Application.Auctions.Commands.PlaceBid;
using AuctionKings.Domain.Aggregates;
using AuctionKings.Domain.ValueObjects;
using AuctionKings.Infrastructure.Persistence;
using AuctionKings.Infrastructure.Repositories;
using AuctionKings.Infrastructure.Services;
using AuctionKings.IntegrationTests.Infrastructure;
using AuctionKings.Shared.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AuctionKings.IntegrationTests.Application;

[Collection("PostgreSql")]
public sealed class PlaceBidConcurrencyTests
{
    private readonly PostgreSqlFixture _fixture;

    public PlaceBidConcurrencyTests(PostgreSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task PlaceBid_ConcurrentSameAmount_OnlyOneBidSucceeds()
    {
        // Arrange
        var auctionId = await SeedActiveAuctionAsync(startingPrice: 100m);
        const int threadCount = 5;
        const decimal bidAmount = 200m; // all bid the same amount; after the first wins, retries fail with BidTooLow

        var sp = BuildServiceProvider();

        // Act: 5 concurrent bids of identical amount, each with a unique idempotency key and bidder
        var tasks = Enumerable
            .Range(0, threadCount)
            .Select(_ =>
                Task.Run(async () =>
                {
                    await using var scope = sp.CreateAsyncScope();
                    var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                    return await sender.Send(
                        new PlaceBidCommand(
                            auctionId,
                            BidderId: Guid.NewGuid(),
                            Amount: bidAmount,
                            Currency: "USD",
                            IdempotencyKey: Guid.NewGuid().ToString()
                        )
                    );
                })
            );

        var results = await Task.WhenAll(tasks);

        // Assert: exactly one bid commits; all others are rejected (BidTooLow on retry)
        var successCount = results.Count(r => r.IsSuccess);
        Assert.Equal(1, successCount);
    }

    [Fact]
    public async Task PlaceBid_SequentialBidsOnSameAuction_AllSucceed()
    {
        // Regression test: AuctionRepository.UpdateAsync used to fail to distinguish a newly
        // added Bid from an already-tracked one once the auction had at least one prior bid,
        // because Entry(existingBid) triggered an implicit DetectChanges() that auto-attached
        // the new Bid as Unchanged before the explicit Detached check ran. EF then issued a
        // phantom UPDATE for a bid row that didn't exist yet, which always affected 0 rows and
        // threw DbUpdateConcurrencyException — every second-or-later bid on an auction failed
        // with Auction.ConcurrencyConflict even with no concurrent writers at all.
        var auctionId = await SeedActiveAuctionAsync(startingPrice: 100m);
        var sp = BuildServiceProvider();

        await using var scope1 = sp.CreateAsyncScope();
        var result1 = await scope1.ServiceProvider.GetRequiredService<ISender>().Send(
            new PlaceBidCommand(auctionId, Guid.NewGuid(), 110m, "USD", Guid.NewGuid().ToString())
        );

        await using var scope2 = sp.CreateAsyncScope();
        var result2 = await scope2.ServiceProvider.GetRequiredService<ISender>().Send(
            new PlaceBidCommand(auctionId, Guid.NewGuid(), 120m, "USD", Guid.NewGuid().ToString())
        );

        await using var scope3 = sp.CreateAsyncScope();
        var result3 = await scope3.ServiceProvider.GetRequiredService<ISender>().Send(
            new PlaceBidCommand(auctionId, Guid.NewGuid(), 130m, "USD", Guid.NewGuid().ToString())
        );

        Assert.True(result1.IsSuccess, result1.IsFailure ? result1.Error.Message : null);
        Assert.True(result2.IsSuccess, result2.IsFailure ? result2.Error.Message : null);
        Assert.True(result3.IsSuccess, result3.IsFailure ? result3.Error.Message : null);
    }

    private async Task<Guid> SeedActiveAuctionAsync(decimal startingPrice)
    {
        await using var context = _fixture.CreateDbContext();
        var auction = Auction.Create(
            "Concurrency Test Auction",
            "Integration test for concurrent bids",
            new Money(startingPrice, "USD"),
            DateTime.UtcNow.AddMinutes(-5),
            AuctionDuration.FromDays(1),
            Guid.NewGuid()
        );
        auction.Activate(DateTime.UtcNow.AddMinutes(-5));
        await context.Auctions.AddAsync(auction);
        await context.SaveChangesAsync();
        return auction.Id;
    }

    private ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddDbContextFactory<AuctionDbContext>(opts =>
            opts.UseNpgsql(_fixture.ConnectionString).UseSnakeCaseNamingConvention(),
            ServiceLifetime.Scoped);
        services.AddScoped<IAuctionRepository, AuctionRepository>();
        services.AddScoped<IBidRepository, BidRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IIdempotencyService, IdempotencyService>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<IRealtimeNotificationService, NoOpNotificationService>();
        services.AddSingleton<ICacheInvalidationService, NoOpCacheInvalidationService>();
        return services.BuildServiceProvider();
    }

    private sealed class NoOpNotificationService : IRealtimeNotificationService
    {
        public Task NotifyBidPlacedAsync(
            Guid auctionId,
            Guid bidId,
            Guid bidderId,
            decimal amount,
            string currency,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;

        public Task NotifyAuctionClosedAsync(
            Guid auctionId,
            Guid? winnerId,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;

        public Task NotifyAuctionExtendedAsync(
            Guid auctionId,
            DateTime newEndTime,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;
    }

    private sealed class NoOpCacheInvalidationService : ICacheInvalidationService
    {
        public Task InvalidateAuctionAsync(
            Guid auctionId,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;

        public Task InvalidateActiveListingsAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task InvalidateUserBidsAsync(
            Guid userId,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;
    }
}

using AuctionKings.Application;
using AuctionKings.Application.Abstractions;
using AuctionKings.Application.Auctions.Commands.PlaceBid;
using AuctionKings.Application.Exceptions;
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
public sealed class PlaceBidIdempotencyTests
{
    private readonly PostgreSqlFixture _fixture;

    public PlaceBidIdempotencyTests(PostgreSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task PlaceBid_SameIdempotencyKey_SecondCallReplaysFirstResult()
    {
        // Arrange
        var auctionId = await SeedActiveAuctionAsync();
        var idempotencyKey = Guid.NewGuid().ToString();
        var bidderId = Guid.NewGuid();

        var sp = BuildServiceProvider();

        // Act: first call executes the command
        await using var scope1 = sp.CreateAsyncScope();
        var sender1 = scope1.ServiceProvider.GetRequiredService<ISender>();
        var first = await sender1.Send(
            new PlaceBidCommand(auctionId, bidderId, 200m, "USD", idempotencyKey)
        );
        Assert.True(first.IsSuccess);

        // Act: a duplicate with the same key gets the stored response replayed, not a second bid
        await using var scope2 = sp.CreateAsyncScope();
        var sender2 = scope2.ServiceProvider.GetRequiredService<ISender>();
        var second = await sender2.Send(
            new PlaceBidCommand(auctionId, bidderId, 200m, "USD", idempotencyKey)
        );

        // Assert
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value, second.Value);

        await using var context = _fixture.CreateDbContext();
        Assert.Equal(1, await context.Bids.CountAsync(b => b.AuctionId == auctionId));
    }

    [Fact]
    public async Task PlaceBid_AbandonedReservation_IsReclaimedByRetry()
    {
        // Arrange: a reservation left behind by a request that crashed mid-flight
        var auctionId = await SeedActiveAuctionAsync();
        var idempotencyKey = Guid.NewGuid().ToString();
        var abandonedAt = DateTime.UtcNow - IdempotencyService.StaleReservationAge - TimeSpan.FromMinutes(1);
        await using (var context = _fixture.CreateDbContext())
        {
            await context.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO idempotency_keys (key, status, response, created_at, expires_at)
                VALUES ({idempotencyKey}, {IdempotencyKey.StatusReserved}, NULL, {abandonedAt}, {abandonedAt.AddHours(24)})
                """);
        }

        var sp = BuildServiceProvider();

        // Act: the client retries with the same key
        await using var scope = sp.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var result = await sender.Send(
            new PlaceBidCommand(auctionId, Guid.NewGuid(), 200m, "USD", idempotencyKey)
        );

        // Assert: the stale reservation was taken over and the command executed
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task PlaceBid_FreshInFlightReservation_IsStillRejected()
    {
        // Arrange: a reservation held by a request that is (presumably) still running
        var auctionId = await SeedActiveAuctionAsync();
        var idempotencyKey = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;
        await using (var context = _fixture.CreateDbContext())
        {
            await context.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO idempotency_keys (key, status, response, created_at, expires_at)
                VALUES ({idempotencyKey}, {IdempotencyKey.StatusReserved}, NULL, {now}, {now.AddHours(24)})
                """);
        }

        var sp = BuildServiceProvider();

        // Act + Assert
        await using var scope = sp.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await Assert.ThrowsAsync<DuplicateCommandException>(() =>
            sender.Send(new PlaceBidCommand(auctionId, Guid.NewGuid(), 200m, "USD", idempotencyKey))
        );
    }

    private async Task<Guid> SeedActiveAuctionAsync()
    {
        await using var context = _fixture.CreateDbContext();
        var auction = Auction.Create(
            "Idempotency Test Auction",
            "Integration test for idempotency",
            new Money(100m, "USD"),
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
        // IdempotencyService needs ICacheService (Redis fast path); a miss-only cache keeps this
        // test on the Postgres path it is meant to exercise.
        services.AddSingleton<ICacheService, MissOnlyCacheService>();
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

    private sealed class MissOnlyCacheService : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class => Task.FromResult<T?>(null);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
            where T : class => Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public async Task<T> GetOrSetAsync<T>(
            string key,
            Func<CancellationToken, Task<T>> factory,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default)
            where T : class => await factory(cancellationToken);
    }
}

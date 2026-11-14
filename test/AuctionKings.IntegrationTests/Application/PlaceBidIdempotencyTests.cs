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
    public async Task PlaceBid_SameIdempotencyKey_AfterCompletion_ReplaysFirstResult()
    {
        // ADR-03: a completed duplicate replays the stored response instead of re-executing.
        var auctionId = await SeedActiveAuctionAsync();
        var idempotencyKey = Guid.NewGuid().ToString();
        var bidderId = Guid.NewGuid();
        var command = new PlaceBidCommand(auctionId, bidderId, 200m, "USD", idempotencyKey);

        await using var sp = BuildServiceProvider();

        await using var scope1 = sp.CreateAsyncScope();
        var first = await scope1.ServiceProvider.GetRequiredService<ISender>().Send(command);
        Assert.True(first.IsSuccess);

        await using var scope2 = sp.CreateAsyncScope();
        var second = await scope2.ServiceProvider.GetRequiredService<ISender>().Send(command);

        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value, second.Value);

        await using var context = _fixture.CreateDbContext();
        Assert.Equal(1, await context.Bids.CountAsync(b => b.AuctionId == auctionId));
    }

    [Fact]
    public async Task PlaceBid_SameIdempotencyKey_WhileInFlight_Throws()
    {
        var auctionId = await SeedActiveAuctionAsync();
        var idempotencyKey = Guid.NewGuid().ToString();

        await using var sp = BuildServiceProvider();

        // Simulate a first request that reserved the key but has not completed yet.
        await using (var reserveScope = sp.CreateAsyncScope())
        {
            var reservation = await reserveScope
                .ServiceProvider.GetRequiredService<IIdempotencyService>()
                .TryReserveAsync(idempotencyKey);
            Assert.Equal(IdempotencyReservationOutcome.Reserved, reservation.Outcome);
        }

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
        ).Value;
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
        services.AddSingleton<ICacheService, NoOpCacheService>();
        return services.BuildServiceProvider();
    }

    private sealed class NoOpNotificationService : IRealtimeNotificationService
    {
        public Task NotifyBidPlacedAsync(
            Guid auctionId,
            Guid bidId,
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

        public Task NotifyAuctionCancelledAsync(
            Guid sellerId,
            Guid auctionId,
            string title,
            DateTime cancelledAt,
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

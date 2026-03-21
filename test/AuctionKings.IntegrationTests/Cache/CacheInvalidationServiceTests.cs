using AuctionKings.Application.Abstractions;
using AuctionKings.Infrastructure.Cache;
using AuctionKings.IntegrationTests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AuctionKings.IntegrationTests.Cache;

[Collection("Redis")]
public sealed class CacheInvalidationServiceTests
{
    private readonly RedisFixture _fixture;

    public CacheInvalidationServiceTests(RedisFixture fixture) => _fixture = fixture;

    private (ICacheInvalidationService invalidation, ICacheService cache) CreateSut()
    {
        var multiplexer = _fixture.CreateMultiplexer();
        var cacheOptions = Options.Create(new CacheOptions());
        var cache = new RedisCacheService(
            multiplexer,
            cacheOptions,
            NullLogger<RedisCacheService>.Instance
        );
        var invalidation = new CacheInvalidationService(cache);
        return (invalidation, cache);
    }

    private sealed record DummyPayload(string Data);

    [Fact]
    public async Task CacheInvalidationService_AfterAuctionUpdate_ShouldInvalidateRelatedKeys()
    {
        var (invalidation, cache) = CreateSut();
        var auctionId = Guid.NewGuid();

        await cache.SetAsync(CacheKeys.AuctionDetail(auctionId), new DummyPayload("detail"));
        await cache.SetAsync(CacheKeys.AuctionBidCount(auctionId), new DummyPayload("count"));

        await invalidation.InvalidateAuctionAsync(auctionId);

        var detail = await cache.GetAsync<DummyPayload>(CacheKeys.AuctionDetail(auctionId));
        var count = await cache.GetAsync<DummyPayload>(CacheKeys.AuctionBidCount(auctionId));

        Assert.Null(detail);
        Assert.Null(count);
    }

    [Fact]
    public async Task CacheInvalidationService_InvalidateActiveListings_ShouldRemoveAllPageVariants()
    {
        var (invalidation, cache) = CreateSut();

        await cache.SetAsync(CacheKeys.ActiveListingsPage(1, 10), new DummyPayload("page1"));
        await cache.SetAsync(CacheKeys.ActiveListingsPage(2, 10), new DummyPayload("page2"));
        await cache.SetAsync(CacheKeys.ActiveListingsPage(3, 20), new DummyPayload("page3"));

        await invalidation.InvalidateActiveListingsAsync();

        var page1 = await cache.GetAsync<DummyPayload>(CacheKeys.ActiveListingsPage(1, 10));
        var page2 = await cache.GetAsync<DummyPayload>(CacheKeys.ActiveListingsPage(2, 10));
        var page3 = await cache.GetAsync<DummyPayload>(CacheKeys.ActiveListingsPage(3, 20));

        Assert.Null(page1);
        Assert.Null(page2);
        Assert.Null(page3);
    }

    [Fact]
    public async Task CacheInvalidationService_InvalidateAuction_ShouldNotAffectOtherAuctionKeys()
    {
        var (invalidation, cache) = CreateSut();
        var auctionId = Guid.NewGuid();
        var otherAuctionId = Guid.NewGuid();
        var payload = new DummyPayload("data");

        await cache.SetAsync(CacheKeys.AuctionDetail(auctionId), payload);
        await cache.SetAsync(CacheKeys.AuctionDetail(otherAuctionId), payload);

        await invalidation.InvalidateAuctionAsync(auctionId);

        var target = await cache.GetAsync<DummyPayload>(CacheKeys.AuctionDetail(auctionId));
        var other = await cache.GetAsync<DummyPayload>(CacheKeys.AuctionDetail(otherAuctionId));

        Assert.Null(target);
        Assert.NotNull(other);
    }

    [Fact]
    public async Task CacheInvalidationService_Invalidate_IsIdempotentWhenKeyNotPresent()
    {
        var (invalidation, _) = CreateSut();
        var auctionId = Guid.NewGuid();

        // Should not throw when key does not exist
        var ex = await Record.ExceptionAsync(
            () => invalidation.InvalidateAuctionAsync(auctionId)
        );

        Assert.Null(ex);
    }
}

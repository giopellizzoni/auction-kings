using System.Globalization;
using AuctionKings.Application.Abstractions;
using AuctionKings.Infrastructure.Cache;
using AuctionKings.IntegrationTests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AuctionKings.IntegrationTests.Cache;

[Collection("Redis")]
public sealed class CacheInvalidationServiceTests
{
    private static readonly TimeSpan OneMinute = TimeSpan.FromMinutes(1);

    private readonly RedisFixture _fixture;

    public CacheInvalidationServiceTests(RedisFixture fixture) => _fixture = fixture;

    private sealed record DummyPayload(string Data);

    private (ICacheInvalidationService Invalidation, ICacheService Cache, IConnectionMultiplexer Multiplexer) CreateSut()
    {
        var multiplexer = _fixture.CreateMultiplexer();
        var cache = new RedisCacheService(
            multiplexer,
            Options.Create(new CacheOptions()),
            NullLogger<RedisCacheService>.Instance);
        return (new CacheInvalidationService(cache), cache, multiplexer);
    }

    // Caches a payload under (scope, key), then reports whether a lookup is still a hit.
    private static async Task<bool> IsCachedAsync(ICacheService cache, string scope, string key)
    {
        var loaded = false;
        await cache.GetOrSetAsync(scope, key, _ =>
        {
            loaded = true;
            return Task.FromResult(new DummyPayload("loaded"));
        }, OneMinute);
        return !loaded;
    }

    private static Task<DummyPayload> CacheAsync(ICacheService cache, string scope, string key) =>
        cache.GetOrSetAsync(scope, key, _ => Task.FromResult(new DummyPayload("cached")), OneMinute);

    [Fact]
    public async Task InvalidateAuction_ShouldClearTheAuctionsDetailsAndEveryBidPage()
    {
        var (invalidation, cache, _) = CreateSut();
        var auctionId = Guid.NewGuid();
        var scope = CacheScopes.Auction(auctionId);
        string[] keys = [$"auction:{auctionId}", $"auction:{auctionId}:bids:1:20", $"auction:{auctionId}:bids:2:20"];
        foreach (var key in keys)
        {
            await CacheAsync(cache, scope, key);
        }

        await invalidation.InvalidateAuctionAsync(auctionId);

        foreach (var key in keys)
        {
            Assert.False(await IsCachedAsync(cache, scope, key));
        }
    }

    [Fact]
    public async Task InvalidateAuction_ShouldNotAffectOtherAuctions()
    {
        var (invalidation, cache, _) = CreateSut();
        var auctionId = Guid.NewGuid();
        var otherAuctionId = Guid.NewGuid();
        await CacheAsync(cache, CacheScopes.Auction(auctionId), $"auction:{auctionId}");
        await CacheAsync(cache, CacheScopes.Auction(otherAuctionId), $"auction:{otherAuctionId}");

        await invalidation.InvalidateAuctionAsync(auctionId);

        Assert.False(await IsCachedAsync(cache, CacheScopes.Auction(auctionId), $"auction:{auctionId}"));
        Assert.True(await IsCachedAsync(cache, CacheScopes.Auction(otherAuctionId), $"auction:{otherAuctionId}"));
    }

    [Fact]
    public async Task InvalidateActiveListings_ShouldRemoveEveryPageVariant()
    {
        var (invalidation, cache, _) = CreateSut();
        string[] keys = ["auctions:active:1:10", "auctions:active:2:10", "auctions:active:3:20"];
        foreach (var key in keys)
        {
            await CacheAsync(cache, CacheScopes.ActiveListings, key);
        }

        await invalidation.InvalidateActiveListingsAsync();

        foreach (var key in keys)
        {
            Assert.False(await IsCachedAsync(cache, CacheScopes.ActiveListings, key));
        }
    }

    [Fact]
    public async Task InvalidateUserBids_ShouldClearThatUsersPagesOnly()
    {
        var (invalidation, cache, _) = CreateSut();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        await CacheAsync(cache, CacheScopes.UserBids(userId), $"user:{userId}:bids:1:20");
        await CacheAsync(cache, CacheScopes.UserBids(otherUserId), $"user:{otherUserId}:bids:1:20");

        await invalidation.InvalidateUserBidsAsync(userId);

        Assert.False(await IsCachedAsync(cache, CacheScopes.UserBids(userId), $"user:{userId}:bids:1:20"));
        Assert.True(await IsCachedAsync(cache, CacheScopes.UserBids(otherUserId), $"user:{otherUserId}:bids:1:20"));
    }

    [Fact]
    public async Task EachInvalidation_ShouldLeaveTheOtherScopesAlone()
    {
        var (invalidation, cache, _) = CreateSut();
        var auctionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await CacheAsync(cache, CacheScopes.Auction(auctionId), $"auction:{auctionId}");
        await CacheAsync(cache, CacheScopes.ActiveListings, "auctions:active:1:20");
        await CacheAsync(cache, CacheScopes.UserBids(userId), $"user:{userId}:bids:1:20");

        await invalidation.InvalidateActiveListingsAsync();

        Assert.True(await IsCachedAsync(cache, CacheScopes.Auction(auctionId), $"auction:{auctionId}"));
        Assert.True(await IsCachedAsync(cache, CacheScopes.UserBids(userId), $"user:{userId}:bids:1:20"));
        Assert.False(await IsCachedAsync(cache, CacheScopes.ActiveListings, "auctions:active:1:20"));
    }

    [Fact]
    public async Task Invalidate_WhenNothingIsCached_ShouldNotThrow()
    {
        var (invalidation, _, _) = CreateSut();

        var ex = await Record.ExceptionAsync(() => invalidation.InvalidateAuctionAsync(Guid.NewGuid()));

        Assert.Null(ex);
    }

    [Fact]
    public async Task Invalidation_ShouldNeverScanTheKeyspace()
    {
        // Regression for #114: invalidation used SCAN over the whole keyspace, three times per bid, and
        // could not work on Redis Cluster. Redis's own command counters show what was actually executed.
        var (invalidation, _, multiplexer) = CreateSut();
        var db = multiplexer.GetDatabase();
        for (var i = 0; i < 500; i++)
        {
            await db.StringSetAsync($"test:unrelated:{Guid.NewGuid()}", "x"); // a keyspace worth scanning
        }

        var before = await KeyEnumerationCallsAsync(multiplexer);

        for (var i = 0; i < 20; i++)
        {
            await invalidation.InvalidateAuctionAsync(Guid.NewGuid());
            await invalidation.InvalidateActiveListingsAsync();
            await invalidation.InvalidateUserBidsAsync(Guid.NewGuid());
        }

        Assert.Equal(before, await KeyEnumerationCallsAsync(multiplexer));
    }

    // Number of SCAN + KEYS commands this Redis has executed so far.
    private static async Task<long> KeyEnumerationCallsAsync(IConnectionMultiplexer multiplexer)
    {
        var server = multiplexer.GetServer(multiplexer.GetEndPoints().First());
        var stats = await server.InfoAsync("commandstats");

        return stats
            .SelectMany(group => group)
            .Where(entry => entry.Key is "cmdstat_scan" or "cmdstat_keys")
            .Sum(entry => long.Parse(
                entry.Value.Split(',')[0].Replace("calls=", string.Empty, StringComparison.Ordinal),
                CultureInfo.InvariantCulture));
    }
}

using AuctionKings.Application.Abstractions;
using AuctionKings.Infrastructure.RateLimiting;
using AuctionKings.IntegrationTests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AuctionKings.IntegrationTests.RateLimiting;

[Collection("Redis")]
public sealed class RedisRateLimiterTests
{
    private readonly RedisFixture _fixture;

    public RedisRateLimiterTests(RedisFixture fixture) => _fixture = fixture;

    private IRateLimiter CreateSut(int maxRequests = 3, int windowSeconds = 10)
    {
        var multiplexer = _fixture.CreateMultiplexer();
        var options = Options.Create(new RateLimitOptions
        {
            MaxRequests = maxRequests,
            Window = TimeSpan.FromSeconds(windowSeconds),
        });
        return new RedisRateLimiter(multiplexer, options, NullLogger<RedisRateLimiter>.Instance);
    }

    [Fact]
    public async Task RedisRateLimiter_WithinLimit_ShouldAllow()
    {
        var sut = CreateSut(maxRequests: 5);
        var key = $"rate:test:{Guid.NewGuid()}";

        var result = await sut.IsAllowedAsync(key);

        Assert.True(result);
    }

    [Fact]
    public async Task RedisRateLimiter_ExceedLimit_ShouldBlock()
    {
        var sut = CreateSut(maxRequests: 2);
        var key = $"rate:test:{Guid.NewGuid()}";

        await sut.IsAllowedAsync(key); // 1st
        await sut.IsAllowedAsync(key); // 2nd

        var result = await sut.IsAllowedAsync(key); // 3rd — over limit

        Assert.False(result);
    }

    [Fact]
    public async Task RedisRateLimiter_ExactlyAtLimit_LastRequestShouldAllow()
    {
        var sut = CreateSut(maxRequests: 3);
        var key = $"rate:test:{Guid.NewGuid()}";

        await sut.IsAllowedAsync(key); // 1st
        await sut.IsAllowedAsync(key); // 2nd
        var result = await sut.IsAllowedAsync(key); // 3rd — exactly at limit

        Assert.True(result);
    }

    [Fact]
    public async Task RedisRateLimiter_LimitPlusOne_ShouldBlock()
    {
        var sut = CreateSut(maxRequests: 3);
        var key = $"rate:test:{Guid.NewGuid()}";

        for (var i = 0; i < 3; i++)
        {
            await sut.IsAllowedAsync(key);
        }

        var result = await sut.IsAllowedAsync(key); // 4th — over limit

        Assert.False(result);
    }

    [Fact]
    public async Task RedisRateLimiter_AfterWindowExpiry_ShouldAllowAgain()
    {
        var sut = CreateSut(maxRequests: 1, windowSeconds: 1);
        var key = $"rate:test:{Guid.NewGuid()}";

        await sut.IsAllowedAsync(key); // consume the 1 allowed slot
        var blockedResult = await sut.IsAllowedAsync(key); // should be blocked
        Assert.False(blockedResult);

        await Task.Delay(TimeSpan.FromSeconds(1.2)); // wait for window to expire

        var result = await sut.IsAllowedAsync(key); // should be allowed again
        Assert.True(result);
    }

    [Fact]
    public async Task RedisRateLimiter_DifferentKeys_ShouldBeIsolated()
    {
        var sut = CreateSut(maxRequests: 1);
        var key1 = $"rate:test:{Guid.NewGuid()}";
        var key2 = $"rate:test:{Guid.NewGuid()}";

        await sut.IsAllowedAsync(key1); // exhaust key1
        var key1Result = await sut.IsAllowedAsync(key1); // blocked
        var key2Result = await sut.IsAllowedAsync(key2); // different key — should be allowed

        Assert.False(key1Result);
        Assert.True(key2Result);
    }

    [Fact]
    public async Task RedisRateLimiter_ConcurrentRequests_ShouldRespectLimit()
    {
        const int limit = 5;
        var sut = CreateSut(maxRequests: limit);
        var key = $"rate:concurrent:{Guid.NewGuid()}";

        var tasks = Enumerable
            .Range(0, limit + 5)
            .Select(_ => sut.IsAllowedAsync(key))
            .ToList();

        var results = await Task.WhenAll(tasks);

        var allowedCount = results.Count(r => r);
        Assert.Equal(limit, allowedCount);
    }
}

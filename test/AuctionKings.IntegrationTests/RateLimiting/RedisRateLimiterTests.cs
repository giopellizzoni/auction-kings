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

    private RedisRateLimiter CreateSut(int maxRequests = 3, int windowSeconds = 10)
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

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task RedisRateLimiter_ExceedLimit_ShouldBlock()
    {
        var sut = CreateSut(maxRequests: 2);
        var key = $"rate:test:{Guid.NewGuid()}";

        await sut.IsAllowedAsync(key); // 1st
        await sut.IsAllowedAsync(key); // 2nd

        var result = await sut.IsAllowedAsync(key); // 3rd — over limit

        Assert.False(result.IsAllowed);
    }

    [Fact]
    public async Task RedisRateLimiter_ExactlyAtLimit_LastRequestShouldAllow()
    {
        var sut = CreateSut(maxRequests: 3);
        var key = $"rate:test:{Guid.NewGuid()}";

        await sut.IsAllowedAsync(key); // 1st
        await sut.IsAllowedAsync(key); // 2nd
        var result = await sut.IsAllowedAsync(key); // 3rd — exactly at limit

        Assert.True(result.IsAllowed);
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

        Assert.False(result.IsAllowed);
    }

    [Fact]
    public async Task RedisRateLimiter_AfterWindowExpiry_ShouldAllowAgain()
    {
        var sut = CreateSut(maxRequests: 1, windowSeconds: 1);
        var key = $"rate:test:{Guid.NewGuid()}";

        await sut.IsAllowedAsync(key); // consume the 1 allowed slot
        var blockedResult = await sut.IsAllowedAsync(key); // should be blocked
        Assert.False(blockedResult.IsAllowed);

        await Task.Delay(TimeSpan.FromSeconds(1.2)); // wait for window to expire

        var result = await sut.IsAllowedAsync(key); // should be allowed again
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task RedisRateLimiter_PerCallLimit_ShouldOverrideTheGlobalLimit()
    {
        var sut = CreateSut(maxRequests: 100); // generous global limit
        var key = $"rate:test:{Guid.NewGuid()}";

        var first = await sut.IsAllowedAsync(key, limit: 2, window: TimeSpan.FromSeconds(10));
        var second = await sut.IsAllowedAsync(key, limit: 2, window: TimeSpan.FromSeconds(10));
        var third = await sut.IsAllowedAsync(key, limit: 2, window: TimeSpan.FromSeconds(10));

        Assert.True(first.IsAllowed);
        Assert.True(second.IsAllowed);
        Assert.False(third.IsAllowed);
        Assert.False(third.IsError);
    }

    [Fact]
    public async Task RedisRateLimiter_PerCallWindow_ShouldOverrideTheGlobalWindow()
    {
        var sut = CreateSut(maxRequests: 1, windowSeconds: 60); // global window far longer than the call's
        var key = $"rate:test:{Guid.NewGuid()}";
        var window = TimeSpan.FromSeconds(1);

        Assert.True((await sut.IsAllowedAsync(key, limit: 1, window)).IsAllowed);
        Assert.False((await sut.IsAllowedAsync(key, limit: 1, window)).IsAllowed);

        await Task.Delay(TimeSpan.FromSeconds(1.2));

        Assert.True((await sut.IsAllowedAsync(key, limit: 1, window)).IsAllowed);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(5, 0)]
    [InlineData(5, -10)]
    public async Task RedisRateLimiter_PerCallOverload_ShouldRejectInvalidArguments(int limit, int windowSeconds)
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            sut.IsAllowedAsync($"rate:test:{Guid.NewGuid()}", limit, TimeSpan.FromSeconds(windowSeconds)));
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

        Assert.False(key1Result.IsAllowed);
        Assert.True(key2Result.IsAllowed);
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

        var allowedCount = results.Count(r => r.IsAllowed);
        Assert.Equal(limit, allowedCount);
    }
}

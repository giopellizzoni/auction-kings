using AuctionKings.Application.Abstractions;
using AuctionKings.Infrastructure.Cache;
using AuctionKings.IntegrationTests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AuctionKings.IntegrationTests.Cache;

[Collection("Redis")]
public sealed class RedisCacheServiceTests
{
    private readonly RedisFixture _fixture;

    public RedisCacheServiceTests(RedisFixture fixture) => _fixture = fixture;

    private ICacheService CreateSut()
    {
        var multiplexer = _fixture.CreateMultiplexer();
        var options = Options.Create(new CacheOptions());
        return new RedisCacheService(multiplexer, options, NullLogger<RedisCacheService>.Instance);
    }

    private sealed record TestPayload(string Name, int Value);

    [Fact]
    public async Task RedisCacheService_SetAndGet_ShouldWork()
    {
        var sut = CreateSut();
        var key = $"test:setget:{Guid.NewGuid()}";
        var payload = new TestPayload("hello", 42);

        await sut.SetAsync(key, payload);
        var result = await sut.GetAsync<TestPayload>(key);

        Assert.NotNull(result);
        Assert.Equal("hello", result.Name);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task RedisCacheService_Expired_ShouldReturnNull()
    {
        var sut = CreateSut();
        var key = $"test:expiry:{Guid.NewGuid()}";
        var payload = new TestPayload("expiring", 1);

        await sut.SetAsync(key, payload, expiry: TimeSpan.FromMilliseconds(100));
        await Task.Delay(200);

        var result = await sut.GetAsync<TestPayload>(key);

        Assert.Null(result);
    }

    [Fact]
    public async Task RedisCacheService_RemoveByPrefix_ShouldRemoveMatchingKeys()
    {
        var sut = CreateSut();
        var prefix = $"test:prefix:{Guid.NewGuid()}";
        var payload = new TestPayload("x", 1);

        await sut.SetAsync($"{prefix}:a", payload);
        await sut.SetAsync($"{prefix}:b", payload);
        await sut.SetAsync($"{prefix}:c", payload);

        await sut.RemoveByPrefixAsync(prefix);

        var a = await sut.GetAsync<TestPayload>($"{prefix}:a");
        var b = await sut.GetAsync<TestPayload>($"{prefix}:b");
        var c = await sut.GetAsync<TestPayload>($"{prefix}:c");

        Assert.Null(a);
        Assert.Null(b);
        Assert.Null(c);
    }

    [Fact]
    public async Task RedisCacheService_RemoveByPrefix_ShouldNotRemoveNonMatchingKeys()
    {
        var sut = CreateSut();
        var prefix = $"test:prefix:{Guid.NewGuid()}";
        var otherKey = $"test:other:{Guid.NewGuid()}";
        var payload = new TestPayload("x", 1);

        await sut.SetAsync($"{prefix}:a", payload);
        await sut.SetAsync(otherKey, payload);

        await sut.RemoveByPrefixAsync(prefix);

        var other = await sut.GetAsync<TestPayload>(otherKey);

        Assert.NotNull(other);
    }

    [Fact]
    public async Task RedisCacheService_GetOrSet_CacheMiss_ShouldCallFactory()
    {
        var sut = CreateSut();
        var key = $"test:getorset:{Guid.NewGuid()}";
        var factoryCallCount = 0;

        var result = await sut.GetOrSetAsync<TestPayload>(
            key,
            _ =>
            {
                factoryCallCount++;
                return Task.FromResult(new TestPayload("factory", 99));
            }
        );

        Assert.Equal(1, factoryCallCount);
        Assert.Equal("factory", result.Name);
    }

    [Fact]
    public async Task RedisCacheService_GetOrSet_CacheHit_ShouldNotCallFactory()
    {
        var sut = CreateSut();
        var key = $"test:getorset:hit:{Guid.NewGuid()}";
        var payload = new TestPayload("cached", 10);
        await sut.SetAsync(key, payload);

        var factoryCallCount = 0;
        var result = await sut.GetOrSetAsync<TestPayload>(
            key,
            _ =>
            {
                factoryCallCount++;
                return Task.FromResult(new TestPayload("factory", 99));
            }
        );

        Assert.Equal(0, factoryCallCount);
        Assert.Equal("cached", result.Name);
    }

    [Fact]
    public async Task RedisCacheService_Remove_ShouldDeleteKey()
    {
        var sut = CreateSut();
        var key = $"test:remove:{Guid.NewGuid()}";
        var payload = new TestPayload("delete-me", 7);

        await sut.SetAsync(key, payload);
        await sut.RemoveAsync(key);
        var result = await sut.GetAsync<TestPayload>(key);

        Assert.Null(result);
    }

    [Fact]
    public async Task RedisCacheService_Set_ShouldOverwriteExistingKey()
    {
        var sut = CreateSut();
        var key = $"test:overwrite:{Guid.NewGuid()}";

        await sut.SetAsync(key, new TestPayload("first", 1));
        await sut.SetAsync(key, new TestPayload("second", 2));

        var result = await sut.GetAsync<TestPayload>(key);

        Assert.NotNull(result);
        Assert.Equal("second", result.Name);
        Assert.Equal(2, result.Value);
    }

    [Fact]
    public async Task RedisCacheService_Get_MissingKey_ShouldReturnNull()
    {
        var sut = CreateSut();
        var key = $"test:missing:{Guid.NewGuid()}";

        var result = await sut.GetAsync<TestPayload>(key);

        Assert.Null(result);
    }

    [Fact]
    public async Task RedisCacheService_Serialization_ShouldRoundTripComplexObject()
    {
        var sut = CreateSut();
        var key = $"test:serial:{Guid.NewGuid()}";
        var payload = new TestPayload("complex", 123);

        await sut.SetAsync(key, payload);
        var result = await sut.GetAsync<TestPayload>(key);

        Assert.NotNull(result);
        Assert.Equal(payload, result);
    }
}

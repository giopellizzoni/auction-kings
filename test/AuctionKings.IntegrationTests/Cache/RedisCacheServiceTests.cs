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

    private RedisCacheService CreateSut()
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

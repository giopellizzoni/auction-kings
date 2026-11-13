using System.Collections.Concurrent;
using AuctionKings.Infrastructure.Cache;
using AuctionKings.IntegrationTests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AuctionKings.IntegrationTests.Cache;

/// <summary>
/// Scoped (versioned) cache entries against a real Redis. See the comment on
/// <see cref="RedisCacheService"/> for how invalidation works.
/// </summary>
[Collection("Redis")]
public sealed class RedisCacheServiceScopedTests
{
    private static readonly TimeSpan OneMinute = TimeSpan.FromMinutes(1);

    private readonly RedisFixture _fixture;

    public RedisCacheServiceScopedTests(RedisFixture fixture) => _fixture = fixture;

    private sealed record TestPayload(string Name, int Value);

    private static string NewScope() => $"test-scope:{Guid.NewGuid()}";

    private static string NewKey() => $"test:scoped:{Guid.NewGuid()}";

    private (RedisCacheService Sut, IDatabase Db) CreateSut()
    {
        var multiplexer = _fixture.CreateMultiplexer();
        var sut = new RedisCacheService(
            multiplexer,
            Options.Create(new CacheOptions()),
            NullLogger<RedisCacheService>.Instance);
        return (sut, multiplexer.GetDatabase());
    }

    [Fact]
    public async Task GetOrSet_OnMiss_ShouldRunTheFactoryOnce_AndThenServeFromCache()
    {
        var (sut, _) = CreateSut();
        var scope = NewScope();
        var key = NewKey();
        var factoryCalls = 0;

        Task<TestPayload> Factory(CancellationToken _)
        {
            factoryCalls++;
            return Task.FromResult(new TestPayload("loaded", 1));
        }

        var first = await sut.GetOrSetAsync(scope, key, Factory, OneMinute);
        var second = await sut.GetOrSetAsync(scope, key, Factory, OneMinute);

        Assert.Equal(1, factoryCalls);
        Assert.Equal(new TestPayload("loaded", 1), first);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task InvalidateScope_ShouldMakeTheNextLookupAMiss()
    {
        var (sut, _) = CreateSut();
        var scope = NewScope();
        var key = NewKey();
        var loads = 0;

        Task<TestPayload> Factory(CancellationToken _) => Task.FromResult(new TestPayload("v", ++loads));

        await sut.GetOrSetAsync(scope, key, Factory, OneMinute);
        await sut.InvalidateScopeAsync(scope);
        var afterInvalidation = await sut.GetOrSetAsync(scope, key, Factory, OneMinute);

        Assert.Equal(2, afterInvalidation.Value);
    }

    [Fact]
    public async Task InvalidateScope_ShouldAffectEveryKeyInTheScope_ButNothingElse()
    {
        var (sut, _) = CreateSut();
        var scope = NewScope();
        var otherScope = NewScope();
        var plainKey = NewKey();
        var inScope = new[] { NewKey(), NewKey(), NewKey() };
        var otherKey = NewKey();

        Task<TestPayload> Old(CancellationToken _) => Task.FromResult(new TestPayload("old", 1));
        Task<TestPayload> Fresh(CancellationToken _) => Task.FromResult(new TestPayload("fresh", 2));

        foreach (var key in inScope)
        {
            await sut.GetOrSetAsync(scope, key, Old, OneMinute);
        }

        await sut.GetOrSetAsync(otherScope, otherKey, Old, OneMinute);
        await sut.SetAsync(plainKey, new TestPayload("plain", 1));

        await sut.InvalidateScopeAsync(scope);

        foreach (var key in inScope)
        {
            Assert.Equal("fresh", (await sut.GetOrSetAsync(scope, key, Fresh, OneMinute)).Name);
        }

        Assert.Equal("old", (await sut.GetOrSetAsync(otherScope, otherKey, Fresh, OneMinute)).Name);
        Assert.Equal("plain", (await sut.GetAsync<TestPayload>(plainKey))!.Name);
    }

    [Fact]
    public async Task InvalidationWhileTheFactoryIsLoading_ShouldNotLeaveTheStaleResultCached()
    {
        // The race this design fixes: a reader loads old data, a writer commits and invalidates, and
        // the reader then writes its old data back. With versioned entries the old data is stored under
        // the version that was current when the lookup started, so it is dead on arrival.
        var (sut, _) = CreateSut();
        var scope = NewScope();
        var key = NewKey();

        var inFlight = await sut.GetOrSetAsync(
            scope,
            key,
            async ct =>
            {
                await sut.InvalidateScopeAsync(scope, ct); // the writer commits and invalidates mid-load
                return new TestPayload("stale", 1);
            },
            OneMinute);
        Assert.Equal("stale", inFlight.Name); // the in-flight caller still gets what it loaded

        var next = await sut.GetOrSetAsync(scope, key, _ => Task.FromResult(new TestPayload("fresh", 2)), OneMinute);

        Assert.Equal("fresh", next.Name);
    }

    [Fact]
    public async Task ConcurrentReadersAndInvalidations_ShouldNeverServeDataOlderThanAFinishedInvalidation()
    {
        // Property test of the guarantee above, under contention. `truth` stands in for the database:
        // the writer commits a new value and only then invalidates. A reader that starts after
        // invalidation N has finished must never be handed a value older than N.
        var (sut, _) = CreateSut();
        var scope = NewScope();
        var key = NewKey();
        long truth = 0;
        long lastFinishedInvalidation = 0;
        var violations = new ConcurrentBag<string>();
        using var writerDone = new CancellationTokenSource();

        var readers = Enumerable.Range(0, 6).Select(_ => Task.Run(async () =>
        {
            while (!writerDone.IsCancellationRequested)
            {
                var floor = Volatile.Read(ref lastFinishedInvalidation);
                var result = await sut.GetOrSetAsync(
                    scope,
                    key,
                    async ct =>
                    {
                        // Read first, then be slow: like a database that answers and then takes a while
                        // to hand the result back, so an invalidation can land in between.
                        var observed = Volatile.Read(ref truth);
                        await Task.Delay(Random.Shared.Next(0, 4), ct);
                        return new TestPayload("v", (int)observed);
                    },
                    OneMinute);

                if (result.Value < floor)
                {
                    violations.Add($"served {result.Value} after invalidation {floor} had finished");
                }
            }
        })).ToArray();

        for (var i = 1; i <= 150; i++)
        {
            Volatile.Write(ref truth, i); // commit
            await sut.InvalidateScopeAsync(scope);
            Volatile.Write(ref lastFinishedInvalidation, i);
            await Task.Delay(1);
        }

        await writerDone.CancelAsync();
        await Task.WhenAll(readers);

        Assert.Empty(violations);
    }

    [Fact]
    public async Task GetOrSetIfFound_WhenTheFactoryReturnsNull_ShouldReturnNull_AndNotCacheIt()
    {
        var (sut, _) = CreateSut();
        var scope = NewScope();
        var key = NewKey();
        var factoryCalls = 0;

        Task<TestPayload?> Factory(CancellationToken _)
        {
            factoryCalls++;
            return Task.FromResult<TestPayload?>(null);
        }

        Assert.Null(await sut.GetOrSetIfFoundAsync(scope, key, Factory, OneMinute));
        Assert.Null(await sut.GetOrSetIfFoundAsync(scope, key, Factory, OneMinute));

        Assert.Equal(2, factoryCalls);
    }

    [Fact]
    public async Task GetOrSet_WhenTheFactoryReturnsNull_ShouldThrow_AndNotCacheIt()
    {
        var (sut, db) = CreateSut();
        var key = NewKey();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GetOrSetAsync<TestPayload>(NewScope(), key, _ => Task.FromResult<TestPayload>(null!), OneMinute));

        Assert.False(await db.KeyExistsAsync(key));
    }

    [Fact]
    public async Task ScopedEntry_ShouldExpireAfterTheRequestedLifetime()
    {
        var (sut, db) = CreateSut();
        var key = NewKey();

        await sut.GetOrSetAsync(NewScope(), key, _ => Task.FromResult(new TestPayload("x", 1)), TimeSpan.FromMinutes(5));

        var ttl = await db.KeyTimeToLiveAsync(key);
        Assert.NotNull(ttl);
        Assert.InRange(ttl.Value, TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task ScopedEntry_ShouldNeverOutliveTheVersionMarker()
    {
        var (sut, db) = CreateSut();
        var key = NewKey();

        await sut.GetOrSetAsync(NewScope(), key, _ => Task.FromResult(new TestPayload("x", 1)), TimeSpan.FromHours(3));

        var ttl = await db.KeyTimeToLiveAsync(key);
        Assert.NotNull(ttl);
        Assert.True(ttl.Value <= RedisCacheService.VersionTtl);
    }

    [Fact]
    public async Task VersionMarker_ShouldExpire_SoMarkersDoNotAccumulate()
    {
        var (sut, db) = CreateSut();
        var scope = NewScope();

        await sut.InvalidateScopeAsync(scope);

        var ttl = await db.KeyTimeToLiveAsync(RedisCacheService.VersionKey(scope));
        Assert.NotNull(ttl);
        Assert.InRange(ttl.Value, RedisCacheService.VersionTtl - TimeSpan.FromMinutes(1), RedisCacheService.VersionTtl);
    }

    [Fact]
    public async Task ExpiredVersionMarker_ShouldNotBringBackEntriesFromBeforeAnInvalidation()
    {
        // Markers are random and never repeat, so when one expires and the scope is invalidated again,
        // an entry written under the earlier marker can never match.
        var (sut, db) = CreateSut();
        var scope = NewScope();
        var key = NewKey();
        await sut.InvalidateScopeAsync(scope);
        await sut.GetOrSetAsync(scope, key, _ => Task.FromResult(new TestPayload("old", 1)), OneMinute);

        await db.KeyDeleteAsync(RedisCacheService.VersionKey(scope)); // the marker expires
        await sut.InvalidateScopeAsync(scope);                        // a later invalidation

        var result = await sut.GetOrSetAsync(scope, key, _ => Task.FromResult(new TestPayload("fresh", 2)), OneMinute);
        Assert.Equal("fresh", result.Name);
    }

    [Fact]
    public async Task ValueWrittenWithoutAVersion_ShouldBeTreatedAsAMiss()
    {
        // For example an entry cached by the previous, unversioned code during a rolling deploy.
        var (sut, _) = CreateSut();
        var key = NewKey();
        await sut.SetAsync(key, new TestPayload("legacy", 1));

        var result = await sut.GetOrSetAsync(NewScope(), key, _ => Task.FromResult(new TestPayload("fresh", 2)), OneMinute);

        Assert.Equal("fresh", result.Name);
    }

    [Fact]
    public async Task CorruptEntry_ShouldBeTreatedAsAMiss_AndRepaired()
    {
        var (sut, db) = CreateSut();
        var scope = NewScope();
        var key = NewKey();
        await db.StringSetAsync(key, "0|{ this is not json", OneMinute);

        var repaired = await sut.GetOrSetAsync(scope, key, _ => Task.FromResult(new TestPayload("fresh", 2)), OneMinute);
        var afterwards = await sut.GetOrSetAsync(scope, key, _ => Task.FromResult(new TestPayload("never", 3)), OneMinute);

        Assert.Equal("fresh", repaired.Name);
        Assert.Equal("fresh", afterwards.Name); // the repaired entry is now a normal hit
    }
}

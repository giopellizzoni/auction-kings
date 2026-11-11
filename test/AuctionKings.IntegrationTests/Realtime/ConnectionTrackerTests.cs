using AuctionKings.Infrastructure.Realtime;
using AuctionKings.IntegrationTests.Infrastructure;
using StackExchange.Redis;

namespace AuctionKings.IntegrationTests.Realtime;

[Collection("Redis")]
public sealed class ConnectionTrackerTests
{
    private static readonly TimeSpan ExpectedTtl = TimeSpan.FromHours(24);

    private readonly RedisFixture _fixture;

    public ConnectionTrackerTests(RedisFixture fixture) => _fixture = fixture;

    private static string Key(Guid userId) => $"connections:{userId}";

    private static void AssertFreshTtl(TimeSpan? ttl)
    {
        Assert.NotNull(ttl);
        Assert.InRange(ttl.Value, ExpectedTtl - TimeSpan.FromMinutes(1), ExpectedTtl);
    }

    [Fact]
    public async Task Track_ShouldStoreConnection_AndSetTtl()
    {
        var multiplexer = _fixture.CreateMultiplexer();
        var tracker = new ConnectionTracker(multiplexer);
        var userId = Guid.NewGuid();

        await tracker.TrackAsync(userId, "conn-1");

        Assert.Equal(["conn-1"], await tracker.GetConnectionsAsync(userId));
        AssertFreshTtl(await multiplexer.GetDatabase().KeyTimeToLiveAsync(Key(userId)));
    }

    [Fact]
    public async Task Track_SameConnectionTwice_ShouldStoreItOnce()
    {
        var tracker = new ConnectionTracker(_fixture.CreateMultiplexer());
        var userId = Guid.NewGuid();

        await tracker.TrackAsync(userId, "conn-1");
        await tracker.TrackAsync(userId, "conn-1");

        Assert.Single(await tracker.GetConnectionsAsync(userId));
    }

    [Fact]
    public async Task Track_ShouldRefreshTtlOfAnExistingKey()
    {
        var multiplexer = _fixture.CreateMultiplexer();
        var db = multiplexer.GetDatabase();
        var tracker = new ConnectionTracker(multiplexer);
        var userId = Guid.NewGuid();
        await tracker.TrackAsync(userId, "conn-1");
        await db.KeyExpireAsync(Key(userId), TimeSpan.FromSeconds(30)); // simulate an old, nearly expired key

        await tracker.TrackAsync(userId, "conn-2");

        AssertFreshTtl(await db.KeyTimeToLiveAsync(Key(userId)));
    }

    [Fact]
    public async Task Untrack_ShouldRemoveOnlyThatConnection_AndRefreshTtlWhileOthersRemain()
    {
        var multiplexer = _fixture.CreateMultiplexer();
        var db = multiplexer.GetDatabase();
        var tracker = new ConnectionTracker(multiplexer);
        var userId = Guid.NewGuid();
        await tracker.TrackAsync(userId, "conn-1");
        await tracker.TrackAsync(userId, "conn-2");
        await db.KeyExpireAsync(Key(userId), TimeSpan.FromSeconds(30));

        await tracker.UntrackAsync(userId, "conn-1");

        Assert.Equal(["conn-2"], await tracker.GetConnectionsAsync(userId));
        AssertFreshTtl(await db.KeyTimeToLiveAsync(Key(userId)));
    }

    [Fact]
    public async Task Untrack_LastConnection_ShouldDeleteTheKey()
    {
        var multiplexer = _fixture.CreateMultiplexer();
        var tracker = new ConnectionTracker(multiplexer);
        var userId = Guid.NewGuid();
        await tracker.TrackAsync(userId, "conn-1");

        await tracker.UntrackAsync(userId, "conn-1");

        Assert.False(await multiplexer.GetDatabase().KeyExistsAsync(Key(userId)));
        Assert.Empty(await tracker.GetConnectionsAsync(userId));
    }

    [Fact]
    public async Task Untrack_UnknownConnection_ShouldBeANoOp_AndNotCreateAKey()
    {
        var multiplexer = _fixture.CreateMultiplexer();
        var tracker = new ConnectionTracker(multiplexer);
        var userId = Guid.NewGuid();

        await tracker.UntrackAsync(userId, "never-tracked");

        Assert.False(await multiplexer.GetDatabase().KeyExistsAsync(Key(userId)));
    }

    [Fact]
    public async Task Untrack_UnknownConnection_ShouldNotDisturbOthers()
    {
        var tracker = new ConnectionTracker(_fixture.CreateMultiplexer());
        var userId = Guid.NewGuid();
        await tracker.TrackAsync(userId, "conn-1");

        await tracker.UntrackAsync(userId, "never-tracked");

        Assert.Equal(["conn-1"], await tracker.GetConnectionsAsync(userId));
    }

    [Fact]
    public async Task GetConnections_ForUnknownUser_ShouldBeEmpty()
    {
        var tracker = new ConnectionTracker(_fixture.CreateMultiplexer());

        Assert.Empty(await tracker.GetConnectionsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Connections_ShouldBeIsolatedPerUser()
    {
        var tracker = new ConnectionTracker(_fixture.CreateMultiplexer());
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        await tracker.TrackAsync(first, "conn-a");
        await tracker.TrackAsync(second, "conn-b");
        await tracker.UntrackAsync(first, "conn-a");

        Assert.Empty(await tracker.GetConnectionsAsync(first));
        Assert.Equal(["conn-b"], await tracker.GetConnectionsAsync(second));
    }

    [Fact]
    public async Task ConcurrentTracks_ShouldAllBeStored_WithATtl()
    {
        var multiplexer = _fixture.CreateMultiplexer();
        var tracker = new ConnectionTracker(multiplexer);
        var userId = Guid.NewGuid();
        var ids = Enumerable.Range(0, 50).Select(i => $"conn-{i}").ToArray();

        await Task.WhenAll(ids.Select(id => tracker.TrackAsync(userId, id)));

        var stored = await tracker.GetConnectionsAsync(userId);
        Assert.Equal(ids.Order(StringComparer.Ordinal), stored.Order(StringComparer.Ordinal));
        AssertFreshTtl(await multiplexer.GetDatabase().KeyTimeToLiveAsync(Key(userId)));
    }

    [Fact]
    public async Task Track_ShouldNeverLeaveAKeyWithoutATtl()
    {
        // Regression for #127. Track used to be SADD then EXPIRE as two commands, so another client
        // could observe the key in between; a crash in that window left it without a TTL forever.
        // A separate connection takes atomic EXISTS + PTTL snapshots while fresh keys are created.
        // PTTL == -1 means "key exists with no expiry". With the old code this fails within a
        // few hundred iterations; the scripted Track makes the window impossible.
        const string snapshotScript = "return {redis.call('EXISTS', KEYS[1]), redis.call('PTTL', KEYS[1])}";
        var tracker = new ConnectionTracker(_fixture.CreateMultiplexer());
        var observer = _fixture.CreateMultiplexer().GetDatabase();
        var keysWithoutTtl = new List<string>();

        for (var i = 0; i < 400; i++)
        {
            var userId = Guid.NewGuid();
            var tracking = tracker.TrackAsync(userId, "conn-1");

            while (true)
            {
                var snapshot = (RedisResult[]?)await observer.ScriptEvaluateAsync(
                    snapshotScript, keys: [new RedisKey(Key(userId))]);
                if ((long)snapshot![0] == 1 && (long)snapshot[1] == -1)
                {
                    keysWithoutTtl.Add(Key(userId));
                }

                if (tracking.IsCompleted)
                {
                    break;
                }
            }

            await tracking;
        }

        Assert.Empty(keysWithoutTtl);
    }
}

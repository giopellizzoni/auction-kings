using AuctionKings.API.Realtime;
using AuctionKings.Application.Abstractions;
using AuctionKings.Infrastructure.Realtime;
using AuctionKings.IntegrationTests.Infrastructure;
using AuctionKings.Shared.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AuctionKings.IntegrationTests.Realtime;

/// <summary>
/// The worker-to-API path: a notification published by the worker side lands on a Redis Stream and the API
/// relay hands it to the SignalR-backed service, exactly once across replicas. Runs against a real Redis.
/// </summary>
[Collection("Redis")]
public sealed class RealtimeStreamRelayTests : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    private readonly RedisFixture _fixture;
    private readonly IConnectionMultiplexer _redis;
    private readonly FixedClock _clock = new(Now);

    public RealtimeStreamRelayTests(RedisFixture fixture)
    {
        _fixture = fixture;
        _redis = fixture.CreateMultiplexer();
    }

    public async Task InitializeAsync() => await _redis.GetDatabase().KeyDeleteAsync(RealtimeFeed.Key);

    public Task DisposeAsync()
    {
        _redis.Dispose();
        return Task.CompletedTask;
    }

    private RedisStreamRealtimeNotificationService Publisher(IDateTimeProvider? clock = null) =>
        new(_redis, clock ?? _clock, NullLogger<RedisStreamRealtimeNotificationService>.Instance);

    private RealtimeStreamRelay Relay(RecordingNotifications sink, RealtimeRelayOptions? options = null) =>
        new(
            _fixture.CreateMultiplexer(),
            sink,
            _clock,
            Options.Create(options ?? new RealtimeRelayOptions()),
            NullLogger<RealtimeStreamRelay>.Instance);

    private static string Raw(int version, string kind, string payloadJson) =>
        "{\"version\":" + version + ",\"kind\":\"" + kind + "\",\"occurredAt\":\"2026-01-15T12:00:00Z\",\"payload\":" + payloadJson + "}";

    private async Task<long> PendingAsync() =>
        (await _redis.GetDatabase().StreamPendingAsync(RealtimeFeed.Key, RealtimeFeed.Group)).PendingMessageCount;

    [Fact]
    public async Task Worker_notifications_reach_the_notification_service_with_their_data()
    {
        var auctionId = Guid.NewGuid();
        var winnerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var publisher = Publisher();
        await publisher.NotifyAuctionClosedAsync(auctionId, winnerId);
        await publisher.NotifyAuctionCancelledAsync(sellerId, auctionId, "Vintage watch", Now);
        var sink = new RecordingNotifications();

        var handled = await Relay(sink).ProcessOnceAsync(CancellationToken.None);

        Assert.Equal(2, handled);
        Assert.Equal([$"closed:{auctionId}:{winnerId}"], sink.Closed);
        Assert.Equal([$"cancelled:{sellerId}:{auctionId}:Vintage watch:{Now:O}"], sink.Cancelled);
        Assert.Equal(0, await PendingAsync());
    }

    [Fact]
    public async Task Every_notification_kind_is_relayed()
    {
        var auctionId = Guid.NewGuid();
        var bidId = Guid.NewGuid();
        var publisher = Publisher();
        await publisher.NotifyBidPlacedAsync(auctionId, bidId, 150.5m, "USD");
        await publisher.NotifyAuctionExtendedAsync(auctionId, Now.AddMinutes(2));
        var sink = new RecordingNotifications();

        await Relay(sink).ProcessOnceAsync(CancellationToken.None);

        Assert.Equal([$"bid:{auctionId}:{bidId}:150.5:USD"], sink.Bids);
        Assert.Equal([$"extended:{auctionId}:{Now.AddMinutes(2):O}"], sink.Extended);
    }

    [Fact]
    public async Task Two_api_replicas_deliver_each_notification_exactly_once()
    {
        const int total = 40;
        var publisher = Publisher();
        for (var i = 0; i < total; i++)
        {
            await publisher.NotifyAuctionClosedAsync(Guid.NewGuid(), null);
        }

        var sinkA = new RecordingNotifications();
        var sinkB = new RecordingNotifications();
        var small = new RealtimeRelayOptions { BatchSize = 5 };
        var replicaA = Relay(sinkA, small);
        var replicaB = Relay(sinkB, small);

        // The replicas take turns, as two API instances polling the same group would.
        while (await replicaA.ProcessOnceAsync(CancellationToken.None) + await replicaB.ProcessOnceAsync(CancellationToken.None) > 0)
        {
        }

        var delivered = sinkA.Closed.Concat(sinkB.Closed).ToList();
        Assert.Equal(total, delivered.Count);
        Assert.Equal(total, delivered.Distinct().Count());
        Assert.NotEmpty(sinkA.Closed);
        Assert.NotEmpty(sinkB.Closed);
        Assert.Equal(0, await PendingAsync());
    }

    [Fact]
    public async Task Malformed_unknown_and_unsupported_entries_are_dropped_and_do_not_block_later_ones()
    {
        var db = _redis.GetDatabase();
        await db.StreamAddAsync(RealtimeFeed.Key, RealtimeFeed.Field, "not json at all");
        await db.StreamAddAsync(RealtimeFeed.Key, "other-field", "x");
        await db.StreamAddAsync(RealtimeFeed.Key, RealtimeFeed.Field, Raw(RealtimeFeed.CurrentVersion + 1, "AuctionClosed", "{}"));
        await db.StreamAddAsync(RealtimeFeed.Key, RealtimeFeed.Field, Raw(RealtimeFeed.CurrentVersion, "SomethingNew", "{}"));
        await db.StreamAddAsync(
            RealtimeFeed.Key,
            RealtimeFeed.Field,
            Raw(RealtimeFeed.CurrentVersion, "AuctionClosed", "{\"auctionId\":\"nope\"}"));
        var auctionId = Guid.NewGuid();
        await Publisher().NotifyAuctionClosedAsync(auctionId, null);
        var sink = new RecordingNotifications();

        await Relay(sink).ProcessOnceAsync(CancellationToken.None);

        Assert.Equal([$"closed:{auctionId}:"], sink.Closed);
        Assert.Equal(0, await PendingAsync());
    }

    [Fact]
    public async Task Stale_entries_are_dropped()
    {
        var old = new FixedClock(Now.AddHours(-1));
        await Publisher(old).NotifyAuctionClosedAsync(Guid.NewGuid(), null);
        var sink = new RecordingNotifications();

        var handled = await Relay(sink).ProcessOnceAsync(CancellationToken.None);

        Assert.Equal(1, handled);
        Assert.Empty(sink.Closed);
        Assert.Equal(0, await PendingAsync());
    }

    [Fact]
    public async Task An_entry_left_unacknowledged_by_a_crashed_replica_is_taken_over()
    {
        var auctionId = Guid.NewGuid();
        await Publisher().NotifyAuctionClosedAsync(auctionId, null);

        // A replica reads the entry and dies before acknowledging it.
        var db = _redis.GetDatabase();
        await db.StreamCreateConsumerGroupAsync(RealtimeFeed.Key, RealtimeFeed.Group, StreamPosition.Beginning, true);
        var read = await db.StreamReadGroupAsync(RealtimeFeed.Key, RealtimeFeed.Group, "crashed", StreamPosition.NewMessages, 10);
        Assert.Single(read);
        Assert.Equal(1, await PendingAsync());

        var sink = new RecordingNotifications();
        var survivor = Relay(sink, new RealtimeRelayOptions { ClaimIdle = TimeSpan.Zero });

        await survivor.ProcessOnceAsync(CancellationToken.None);

        Assert.Equal([$"closed:{auctionId}:"], sink.Closed);
        Assert.Equal(0, await PendingAsync());
    }

    [Fact]
    public async Task A_failing_delivery_is_dropped_so_the_stream_does_not_wedge()
    {
        var publisher = Publisher();
        await publisher.NotifyAuctionClosedAsync(Guid.NewGuid(), null);
        var survivorId = Guid.NewGuid();
        await publisher.NotifyAuctionClosedAsync(survivorId, null);
        var sink = new RecordingNotifications { FailFirstClosed = true };

        await Relay(sink).ProcessOnceAsync(CancellationToken.None);

        Assert.Equal([$"closed:{survivorId}:"], sink.Closed);
        Assert.Equal(0, await PendingAsync());
    }

    [Fact]
    public async Task The_stream_is_trimmed_to_its_approximate_cap()
    {
        var db = _redis.GetDatabase();
        var publisher = Publisher();
        var total = RealtimeFeed.MaxLength + 1_000;

        // Pipelined in batches: the multiplexer sends them without a round trip each.
        for (var sent = 0; sent < total; sent += 500)
        {
            await Task.WhenAll(
                Enumerable.Range(0, Math.Min(500, total - sent)).Select(_ => publisher.NotifyAuctionClosedAsync(Guid.NewGuid(), null)));
        }

        // Approximate trimming only drops whole nodes, so allow slack above the cap. Without trimming the
        // stream would hold all `total` entries and this bound would fail.
        var length = await db.StreamLengthAsync(RealtimeFeed.Key);
        Assert.InRange(length, RealtimeFeed.MaxLength, RealtimeFeed.MaxLength + 500);
        Assert.True(length < total);
    }

    private async Task<string[]> ConsumerNamesAsync() =>
        (await _redis.GetDatabase().StreamConsumerInfoAsync(RealtimeFeed.Key, RealtimeFeed.Group))
            .Select(c => c.Name)
            .ToArray();

    [Fact]
    public async Task Stopping_a_replica_removes_its_consumer_when_nothing_is_pending()
    {
        await Publisher().NotifyAuctionClosedAsync(Guid.NewGuid(), null);
        var relay = Relay(new RecordingNotifications());
        await relay.ProcessOnceAsync(CancellationToken.None);
        Assert.Contains(relay.ConsumerName, await ConsumerNamesAsync());

        await relay.StopAsync(CancellationToken.None);

        Assert.DoesNotContain(relay.ConsumerName, await ConsumerNamesAsync());
    }

    [Fact]
    public async Task Stopping_a_replica_keeps_its_consumer_while_it_owns_unacknowledged_entries()
    {
        var publisher = Publisher();
        await publisher.NotifyAuctionClosedAsync(Guid.NewGuid(), null);
        var relay = Relay(new RecordingNotifications());
        await relay.ProcessOnceAsync(CancellationToken.None);

        // The replica read another entry and was stopped before acknowledging it.
        await publisher.NotifyAuctionClosedAsync(Guid.NewGuid(), null);
        var db = _redis.GetDatabase();
        var read = await db.StreamReadGroupAsync(
            RealtimeFeed.Key,
            RealtimeFeed.Group,
            relay.ConsumerName,
            StreamPosition.NewMessages,
            10);
        Assert.Single(read);

        await relay.StopAsync(CancellationToken.None);

        // Deleting the consumer would drop its pending entry for good; it must stay claimable.
        Assert.Contains(relay.ConsumerName, await ConsumerNamesAsync());
        Assert.Equal(1, await PendingAsync());
    }

    private sealed class FixedClock(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class RecordingNotifications : IRealtimeNotificationService
    {
        private bool _failed;

        public bool FailFirstClosed { get; init; }
        public List<string> Bids { get; } = [];
        public List<string> Closed { get; } = [];
        public List<string> Extended { get; } = [];
        public List<string> Cancelled { get; } = [];

        public Task NotifyBidPlacedAsync(
            Guid auctionId,
            Guid bidId,
            decimal amount,
            string currency,
            CancellationToken cancellationToken = default)
        {
            Bids.Add($"bid:{auctionId}:{bidId}:{amount}:{currency}");
            return Task.CompletedTask;
        }

        public Task NotifyAuctionClosedAsync(Guid auctionId, Guid? winnerId, CancellationToken cancellationToken = default)
        {
            if (FailFirstClosed && !_failed)
            {
                _failed = true;
                throw new InvalidOperationException("delivery failed");
            }

            Closed.Add($"closed:{auctionId}:{winnerId}");
            return Task.CompletedTask;
        }

        public Task NotifyAuctionExtendedAsync(Guid auctionId, DateTime newEndTime, CancellationToken cancellationToken = default)
        {
            Extended.Add($"extended:{auctionId}:{newEndTime:O}");
            return Task.CompletedTask;
        }

        public Task NotifyAuctionCancelledAsync(
            Guid sellerId,
            Guid auctionId,
            string title,
            DateTime cancelledAt,
            CancellationToken cancellationToken = default)
        {
            Cancelled.Add($"cancelled:{sellerId}:{auctionId}:{title}:{cancelledAt:O}");
            return Task.CompletedTask;
        }
    }
}

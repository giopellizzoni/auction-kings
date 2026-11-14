using AuctionKings.Infrastructure.Realtime;
using AuctionKings.Shared.Common;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StackExchange.Redis;

namespace AuctionKings.UnitTests.Infrastructure;

public class RedisStreamRealtimeNotificationServiceTests
{
    private static readonly DateTime Now = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Notify_WhenRedisIsDown_ShouldNotThrow()
    {
        // The operation that triggered the notification already succeeded; losing the push must not undo it.
        var redis = Substitute.For<IConnectionMultiplexer>();
        redis.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Throws(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        var service = new RedisStreamRealtimeNotificationService(
            redis,
            clock,
            NullLogger<RedisStreamRealtimeNotificationService>.Instance);

        await service.NotifyAuctionClosedAsync(Guid.NewGuid(), null);
        await service.NotifyAuctionCancelledAsync(Guid.NewGuid(), Guid.NewGuid(), "Title", Now);
        await service.NotifyBidPlacedAsync(Guid.NewGuid(), Guid.NewGuid(), 10m, "USD");
        await service.NotifyAuctionExtendedAsync(Guid.NewGuid(), Now);
    }

    [Fact]
    public void Envelope_ShouldRoundTrip_AndRejectGarbage()
    {
        var payload = new AuctionCancelledPayload(Guid.NewGuid(), Guid.NewGuid(), "Vintage watch", Now);

        var json = RealtimeFeed.Serialize(RealtimeKinds.AuctionCancelled, Now, payload);
        var envelope = RealtimeFeed.TryParse(json);

        Assert.NotNull(envelope);
        Assert.Equal(RealtimeFeed.CurrentVersion, envelope.Version);
        Assert.Equal(RealtimeKinds.AuctionCancelled, envelope.Kind);
        Assert.Equal(Now, envelope.OccurredAt);
        Assert.Equal(payload, RealtimeFeed.TryReadPayload<AuctionCancelledPayload>(envelope));

        Assert.Null(RealtimeFeed.TryParse(null));
        Assert.Null(RealtimeFeed.TryParse(""));
        Assert.Null(RealtimeFeed.TryParse("{not json"));
    }
}

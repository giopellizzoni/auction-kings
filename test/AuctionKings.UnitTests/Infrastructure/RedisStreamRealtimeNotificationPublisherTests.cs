using AuctionKings.Infrastructure.Realtime;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StackExchange.Redis;

namespace AuctionKings.UnitTests.Infrastructure;

public sealed class RedisStreamRealtimeNotificationPublisherTests
{
    private readonly IConnectionMultiplexer _redis = Substitute.For<IConnectionMultiplexer>();
    private readonly IDatabase _database = Substitute.For<IDatabase>();
    private readonly RedisStreamRealtimeNotificationPublisher _publisher;

    public RedisStreamRealtimeNotificationPublisherTests()
    {
        _redis.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(_database);
        _publisher = new RedisStreamRealtimeNotificationPublisher(
            _redis,
            NullLogger<RedisStreamRealtimeNotificationPublisher>.Instance);
    }

    [Fact]
    public async Task NotifyAuctionClosedAsync_ShouldAppendToStream()
    {
        var auctionId = Guid.NewGuid();
        var winnerId = Guid.NewGuid();
        RedisValue captured = default;
        _database
            .StreamAddAsync(
                Arg.Any<RedisKey>(),
                Arg.Any<RedisValue>(),
                Arg.Do<RedisValue>(v => captured = v),
                Arg.Any<RedisValue?>(),
                Arg.Any<long?>(),
                Arg.Any<bool>(),
                Arg.Any<long?>(),
                Arg.Any<StreamTrimMode>(),
                Arg.Any<CommandFlags>())
            .Returns(new RedisValue("1-0"));

        await _publisher.NotifyAuctionClosedAsync(auctionId, winnerId);

        var notification = RealtimeNotificationChannel.Deserialize(captured);
        Assert.NotNull(notification);
        Assert.Equal(RealtimeNotificationType.AuctionClosed, notification.Type);
        Assert.Equal(auctionId, notification.AuctionId);
        Assert.Equal(winnerId, notification.WinnerId);
    }

    [Fact]
    public async Task Notify_WhenRedisFails_ShouldNotThrow()
    {
        _database
            .StreamAddAsync(
                Arg.Any<RedisKey>(),
                Arg.Any<RedisValue>(),
                Arg.Any<RedisValue>(),
                Arg.Any<RedisValue?>(),
                Arg.Any<long?>(),
                Arg.Any<bool>(),
                Arg.Any<long?>(),
                Arg.Any<StreamTrimMode>(),
                Arg.Any<CommandFlags>())
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));

        var exception = await Record.ExceptionAsync(
            () => _publisher.NotifyAuctionExtendedAsync(Guid.NewGuid(), DateTime.UtcNow));

        Assert.Null(exception);
    }
}

using AuctionKings.Infrastructure.Realtime;
using NSubstitute;
using StackExchange.Redis;

namespace AuctionKings.UnitTests.Infrastructure;

public sealed class ConnectionTrackerTests
{
    private readonly IConnectionMultiplexer _redis = Substitute.For<IConnectionMultiplexer>();
    private readonly IDatabase _database = Substitute.For<IDatabase>();
    private readonly ConnectionTracker _tracker;

    public ConnectionTrackerTests()
    {
        _redis.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(_database);
        _tracker = new ConnectionTracker(_redis);
    }

    [Fact]
    public async Task ConnectionTracker_Connect_ShouldTrackUser()
    {
        var userId = Guid.NewGuid();
        const string connectionId = "conn-1";

        await _tracker.TrackAsync(userId, connectionId);

        await _database.Received(1).SetAddAsync(
            Arg.Is<RedisKey>(k => k == $"connections:{userId}"),
            Arg.Is<RedisValue>(v => v == connectionId),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task ConnectionTracker_Connect_ShouldSetTtl()
    {
        var userId = Guid.NewGuid();

        await _tracker.TrackAsync(userId, "conn-1");

        await _database.Received(1).KeyExpireAsync(
            Arg.Is<RedisKey>(k => k == $"connections:{userId}"),
            Arg.Is<TimeSpan>(t => t == TimeSpan.FromHours(24)),
            Arg.Any<ExpireWhen>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task ConnectionTracker_Disconnect_ShouldRemoveConnection()
    {
        var userId = Guid.NewGuid();
        const string connectionId = "conn-1";
        _database.SetLengthAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(0);

        await _tracker.UntrackAsync(userId, connectionId);

        await _database.Received(1).SetRemoveAsync(
            Arg.Is<RedisKey>(k => k == $"connections:{userId}"),
            Arg.Is<RedisValue>(v => v == connectionId),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task ConnectionTracker_Disconnect_WhenConnectionsRemain_ShouldRefreshTtl()
    {
        var userId = Guid.NewGuid();
        _database.SetLengthAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(1);

        await _tracker.UntrackAsync(userId, "conn-1");

        await _database.Received(1).KeyExpireAsync(
            Arg.Is<RedisKey>(k => k == $"connections:{userId}"),
            Arg.Is<TimeSpan>(t => t == TimeSpan.FromHours(24)),
            Arg.Any<ExpireWhen>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task ConnectionTracker_GetConnections_ShouldReturnAllConnectionIds()
    {
        var userId = Guid.NewGuid();
        var members = new RedisValue[] { "conn-1", "conn-2" };
        _database.SetMembersAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(members);

        var result = await _tracker.GetConnectionsAsync(userId);

        Assert.Equal(2, result.Count);
        Assert.Contains("conn-1", result);
        Assert.Contains("conn-2", result);
    }
}

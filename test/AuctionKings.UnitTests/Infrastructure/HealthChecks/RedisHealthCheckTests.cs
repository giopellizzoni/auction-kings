using AuctionKings.Infrastructure.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StackExchange.Redis;

namespace AuctionKings.UnitTests.Infrastructure.HealthChecks;

public sealed class RedisHealthCheckTests
{
    private static HealthCheckContext BuildContext(IHealthCheck check) =>
        new() { Registration = new HealthCheckRegistration("redis", check, HealthStatus.Unhealthy, []) };

    [Fact]
    public async Task CheckHealthAsync_WhenPingSucceeds_ReturnsHealthy()
    {
        var db = Substitute.For<IDatabase>();
        db.PingAsync().Returns(TimeSpan.FromMilliseconds(1));
        var multiplexer = Substitute.For<IConnectionMultiplexer>();
        multiplexer.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(db);
        var check = new RedisHealthCheck(multiplexer);

        var result = await check.CheckHealthAsync(BuildContext(check));

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenPingThrows_ReturnsUnhealthy()
    {
        var db = Substitute.For<IDatabase>();
        db.PingAsync().ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));
        var multiplexer = Substitute.For<IConnectionMultiplexer>();
        multiplexer.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(db);
        var check = new RedisHealthCheck(multiplexer);

        var result = await check.CheckHealthAsync(BuildContext(check));

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }
}

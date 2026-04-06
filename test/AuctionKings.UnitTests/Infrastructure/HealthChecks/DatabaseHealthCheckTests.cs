using AuctionKings.Infrastructure.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace AuctionKings.UnitTests.Infrastructure.HealthChecks;

public sealed class DatabaseHealthCheckTests
{
    private static HealthCheckContext BuildContext(IHealthCheck check) =>
        new() { Registration = new HealthCheckRegistration("database", check, HealthStatus.Unhealthy, []) };

    [Fact]
    public async Task CheckHealthAsync_WhenCanConnect_ReturnsHealthy()
    {
        var ping = Substitute.For<IDatabasePingService>();
        ping.CanConnectAsync(Arg.Any<CancellationToken>()).Returns(true);
        var check = new DatabaseHealthCheck(ping);

        var result = await check.CheckHealthAsync(BuildContext(check));

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenCannotConnect_ReturnsUnhealthy()
    {
        var ping = Substitute.For<IDatabasePingService>();
        ping.CanConnectAsync(Arg.Any<CancellationToken>()).Returns(false);
        var check = new DatabaseHealthCheck(ping);

        var result = await check.CheckHealthAsync(BuildContext(check));

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenThrows_ReturnsUnhealthy()
    {
        var ping = Substitute.For<IDatabasePingService>();
        ping.CanConnectAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("DB down"));
        var check = new DatabaseHealthCheck(ping);

        var result = await check.CheckHealthAsync(BuildContext(check));

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }
}

using AuctionKings.Infrastructure.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace AuctionKings.UnitTests.Infrastructure.HealthChecks;

public sealed class OutboxHealthCheckTests
{
    private static HealthCheckContext BuildContext(IHealthCheck check) =>
        new() { Registration = new HealthCheckRegistration("outbox", check, HealthStatus.Degraded, []) };

    [Fact]
    public async Task CheckHealthAsync_NoStuckMessages_ReturnsHealthy()
    {
        var service = Substitute.For<IOutboxHealthService>();
        service.CountStuckMessagesAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(0);
        var check = new OutboxHealthCheck(service);

        var result = await check.CheckHealthAsync(BuildContext(check));

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_StuckMessages_ReturnsDegraded()
    {
        var service = Substitute.For<IOutboxHealthService>();
        service.CountStuckMessagesAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(3);
        var check = new OutboxHealthCheck(service);

        var result = await check.CheckHealthAsync(BuildContext(check));

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("3", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenThrows_ReturnsUnhealthy()
    {
        var service = Substitute.For<IOutboxHealthService>();
        service.CountStuckMessagesAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("DB down"));
        var check = new OutboxHealthCheck(service);

        var result = await check.CheckHealthAsync(BuildContext(check));

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}

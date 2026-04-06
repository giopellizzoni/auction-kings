using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AuctionKings.Infrastructure.HealthChecks;

internal sealed class OutboxHealthCheck(IOutboxHealthService outboxHealth) : IHealthCheck
{
    private static readonly TimeSpan StuckThreshold = TimeSpan.FromMinutes(5);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var threshold = DateTime.UtcNow - StuckThreshold;
            var count = await outboxHealth.CountStuckMessagesAsync(threshold, cancellationToken)
                .ConfigureAwait(false);

            return count > 0
                ? HealthCheckResult.Degraded(
                    $"{count} outbox message(s) stuck for more than {StuckThreshold.TotalMinutes} minutes.")
                : HealthCheckResult.Healthy("Outbox processor is healthy.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Outbox health check failed.", ex);
        }
    }
}

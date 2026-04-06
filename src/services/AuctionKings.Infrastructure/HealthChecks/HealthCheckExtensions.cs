using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AuctionKings.Infrastructure.HealthChecks;

public static class HealthCheckExtensions
{
    public static IHealthChecksBuilder AddInfrastructureHealthChecks(this IHealthChecksBuilder builder)
    {
        builder.AddCheck<DatabaseHealthCheck>("database", HealthStatus.Unhealthy, ["ready", "db"]);
        builder.AddCheck<RedisHealthCheck>("redis", HealthStatus.Unhealthy, ["ready"]);
        builder.AddCheck<OutboxHealthCheck>("outbox", HealthStatus.Degraded, ["ready"]);
        return builder;
    }
}

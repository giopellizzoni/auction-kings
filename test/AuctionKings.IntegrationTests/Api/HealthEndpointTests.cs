using System.Net;
using AuctionKings.IntegrationTests.Infrastructure;

namespace AuctionKings.IntegrationTests.Api;

/// <summary>
/// Liveness must not depend on PostgreSQL or Redis: ECS and docker compose restart a container whose
/// liveness probe fails, so a dependency outage would otherwise turn into a restart loop of healthy tasks.
/// </summary>
[Collection("AuthApi")]
public sealed class HealthEndpointTests(AuthApiFactory factory)
{
    // Nothing listens on port 1, so connections are refused immediately.
    private const string UnreachableDatabase =
        "Host=127.0.0.1;Port=1;Database=unreachable;Username=x;Password=x;Timeout=2";

    [Fact]
    public async Task Live_is_healthy_and_ready_is_unhealthy_when_the_database_is_down()
    {
        using var hostWithoutDatabase = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", UnreachableDatabase);

            // Nothing to migrate against; startup must still succeed so the probes can answer.
            builder.UseSetting("Database:MigrateOnStartup", "false");
        });
        using var client = hostWithoutDatabase.CreateClient();

        var live = await client.GetAsync("/health/live");
        var ready = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal("Healthy", await live.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
    }

    [Fact]
    public async Task Live_and_ready_are_healthy_when_dependencies_are_up()
    {
        using var client = factory.CreateClient();

        var live = await client.GetAsync("/health/live");
        var ready = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
    }
}

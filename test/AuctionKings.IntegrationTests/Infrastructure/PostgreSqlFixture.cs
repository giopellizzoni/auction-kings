using AuctionKings.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace AuctionKings.IntegrationTests.Infrastructure;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("auction_kings_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public AuctionDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AuctionDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AuctionDbContext(options);
    }

    /// <summary>
    /// A context configured like the application's (<c>EnableRetryOnFailure</c>), for code that must work
    /// under NpgsqlRetryingExecutionStrategy — e.g. explicit transactions.
    /// </summary>
    public AuctionDbContext CreateRetryingDbContext()
    {
        var options = new DbContextOptionsBuilder<AuctionDbContext>()
            .UseNpgsql(_container.GetConnectionString(), npgsql => npgsql.EnableRetryOnFailure(3))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AuctionDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // Build the schema from the real migrations, not EnsureCreated(): a migration EF cannot
        // discover (or one that drifts from the model) must fail these tests, not production.
        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

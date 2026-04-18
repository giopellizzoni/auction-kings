using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AuctionKings.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> when running migrations.
/// Reads the connection string from the <c>ConnectionStrings__DefaultConnection</c>
/// environment variable so credentials are never hardcoded.
/// </summary>
internal sealed class AuctionDbContextFactory : IDesignTimeDbContextFactory<AuctionDbContext>
{
    public AuctionDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=AuctionKingsDB;Username=dev;Password=dev";

        var options = new DbContextOptionsBuilder<AuctionDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsAssembly(typeof(AuctionDbContext).Assembly.FullName)
            )
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AuctionDbContext(options);
    }
}

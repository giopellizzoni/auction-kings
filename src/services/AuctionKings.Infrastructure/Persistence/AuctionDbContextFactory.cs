using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AuctionKings.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> when running migrations.
/// </summary>
internal sealed class AuctionDbContextFactory : IDesignTimeDbContextFactory<AuctionDbContext>
{
    public AuctionDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AuctionDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=AuctionKingsDB;Username=dev;Password=dev",
                npgsql => npgsql.MigrationsAssembly(typeof(AuctionDbContext).Assembly.FullName)
            )
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AuctionDbContext(options);
    }
}

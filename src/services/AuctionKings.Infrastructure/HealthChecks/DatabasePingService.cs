using AuctionKings.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace AuctionKings.Infrastructure.HealthChecks;

internal sealed class DatabasePingService(IServiceScopeFactory scopeFactory) : IDatabasePingService
{
    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuctionDbContext>();
        return await context.Database.CanConnectAsync(cancellationToken);
    }
}

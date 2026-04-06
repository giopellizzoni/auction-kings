using AuctionKings.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AuctionKings.Infrastructure.HealthChecks;

internal sealed class OutboxHealthService(IServiceScopeFactory scopeFactory) : IOutboxHealthService
{
    public async Task<int> CountStuckMessagesAsync(DateTime olderThan, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuctionDbContext>();
        return await context.OutboxMessages
            .CountAsync(m => m.ProcessedAt == null && !m.Failed && m.OccurredOn < olderThan, cancellationToken)
            .ConfigureAwait(false);
    }
}

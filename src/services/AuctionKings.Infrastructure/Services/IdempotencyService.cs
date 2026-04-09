using AuctionKings.Application.Abstractions;
using AuctionKings.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuctionKings.Infrastructure.Services;

internal sealed class IdempotencyService : IIdempotencyService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    private readonly IDbContextFactory<AuctionDbContext> _contextFactory;

    public IdempotencyService(IDbContextFactory<AuctionDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.IdempotencyKeys.AnyAsync(
            x => x.Key == key && x.ExpiresAt > DateTime.UtcNow,
            cancellationToken
        );
    }

    public async Task StoreAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var idempotencyKey = IdempotencyKey.Create(key, DateTime.UtcNow, Ttl);
        context.IdempotencyKeys.Add(idempotencyKey);
        await context.SaveChangesAsync(cancellationToken);
    }
}

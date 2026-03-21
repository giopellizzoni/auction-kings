using AuctionKings.Application.Abstractions;
using AuctionKings.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuctionKings.Infrastructure.Services;

internal sealed class IdempotencyService : IIdempotencyService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    private readonly AuctionDbContext _context;

    public IdempotencyService(AuctionDbContext context)
    {
        _context = context;
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        return await _context.IdempotencyKeys.AnyAsync(
            x => x.Key == key && x.ExpiresAt > DateTime.UtcNow,
            cancellationToken
        );
    }

    public async Task StoreAsync(string key, CancellationToken cancellationToken = default)
    {
        var idempotencyKey = IdempotencyKey.Create(key, DateTime.UtcNow, Ttl);
        _context.IdempotencyKeys.Add(idempotencyKey);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

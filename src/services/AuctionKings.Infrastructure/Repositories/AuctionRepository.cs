using AuctionKings.Application.Abstractions;
using AuctionKings.Domain.Aggregates;
using AuctionKings.Domain.Enums;
using AuctionKings.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuctionKings.Infrastructure.Repositories;

internal sealed class AuctionRepository : IAuctionRepository
{
    private readonly AuctionDbContext _context;

    public AuctionRepository(AuctionDbContext context) => _context = context;

    public async Task<Auction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Auctions
            .Include(a => a.Bids)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Auction>> GetActiveAuctionsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default
    )
    {
        return await _context.Auctions
            .Where(a => a.Status == AuctionStatus.Active)
            .OrderBy(a => a.EndTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountActiveAuctionsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Auctions
            .CountAsync(a => a.Status == AuctionStatus.Active, cancellationToken);
    }

    public async Task AddAsync(Auction auction, CancellationToken cancellationToken = default)
    {
        await _context.Auctions.AddAsync(auction, cancellationToken);
    }

    public Task UpdateAsync(Auction auction, CancellationToken cancellationToken = default)
    {
        // The auction was loaded from this context and is already tracked as Unchanged.
        // Calling DbSet.Update() on a tracked entity resets the original-values snapshot, which
        // corrupts the Version concurrency token (WHERE version = @original would use the new value).
        // Instead, rely on snapshot change tracking for scalar property changes (Version, CurrentPrice, etc.)
        // and explicitly register any new Bid entities — EF Core cannot distinguish new (Added)
        // from existing (Modified) for entities with client-generated Guid keys discovered via navigation.
        foreach (var bid in auction.Bids)
        {
            if (_context.Entry(bid).State == EntityState.Detached)
            {
                _context.Bids.Add(bid);
            }
        }

        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Auction>> GetExpiredAuctionsAsync(
        DateTime now,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        return await _context.Auctions
            .Where(a => a.Status == AuctionStatus.Active && a.EndTime <= now)
            .OrderBy(a => a.EndTime)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Auction>> GetPendingAuctionsToActivateAsync(
        DateTime now,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        return await _context.Auctions
            .Where(a => a.Status == AuctionStatus.Pending && a.StartTime <= now)
            .OrderBy(a => a.StartTime)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }
}

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
        //
        // AutoDetectChangesEnabled must be off for this loop: Entry(...) triggers an implicit
        // DetectChanges() pass, and on an auction with an existing (already-tracked) bid, that
        // pass discovers any newly-added Bid reachable from the Bids navigation and auto-attaches
        // it as Unchanged (a client-generated Guid key looks "permanent" to EF) *before* this
        // loop's own Detached check ever reaches it. The result: the new bid never gets Added,
        // EF emits an UPDATE for a bid row that doesn't exist yet, and that phantom UPDATE
        // (0 rows affected) always throws DbUpdateConcurrencyException — i.e. every bid after
        // the first one on a given auction was failing with a 409, with no real concurrent writer.
        var changeTracker = _context.ChangeTracker;
        var autoDetectChangesEnabled = changeTracker.AutoDetectChangesEnabled;
        changeTracker.AutoDetectChangesEnabled = false;
        try
        {
            foreach (var bid in auction.Bids)
            {
                if (_context.Entry(bid).State == EntityState.Detached)
                {
                    _context.Bids.Add(bid);
                }
            }
        }
        finally
        {
            changeTracker.AutoDetectChangesEnabled = autoDetectChangesEnabled;
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
}

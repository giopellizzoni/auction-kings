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
        _context.Auctions.Update(auction);
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

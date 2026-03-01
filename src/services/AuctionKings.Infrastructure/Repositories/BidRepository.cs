using AuctionKings.Application.Abstractions;
using AuctionKings.Domain.Aggregates;
using AuctionKings.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuctionKings.Infrastructure.Repositories;

internal sealed class BidRepository : IBidRepository
{
    private readonly AuctionDbContext _context;

    public BidRepository(AuctionDbContext context) => _context = context;

    public async Task<IReadOnlyList<Bid>> GetByAuctionIdAsync(
        Guid auctionId,
        CancellationToken cancellationToken = default
    )
    {
        return await _context.Bids
            .Where(b => b.AuctionId == auctionId)
            .OrderByDescending(b => b.PlacedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Bid> Items, int TotalCount)> GetByAuctionIdPagedAsync(
        Guid auctionId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default
    )
    {
        var query = _context.Bids.Where(b => b.AuctionId == auctionId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(b => b.PlacedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(Bid bid, CancellationToken cancellationToken = default)
    {
        await _context.Bids.AddAsync(bid, cancellationToken);
    }
}

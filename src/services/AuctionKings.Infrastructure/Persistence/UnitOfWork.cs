using AuctionKings.Application.Abstractions;
using AuctionKings.Application.Exceptions;
using AuctionKings.Shared.Common;
using Microsoft.EntityFrameworkCore;

namespace AuctionKings.Infrastructure.Persistence;

internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly AuctionDbContext _context;

    public UnitOfWork(AuctionDbContext context) => _context = context;

    public async Task<int> CommitAsync(CancellationToken cancellationToken = default)
    {
        var outboxMessages = _context.ChangeTracker
            .Entries<AggregateRoot>()
            .Where(entry => entry.Entity.GetDomainEvents().Count > 0)
            .Select(entry => entry.Entity)
            .SelectMany(aggregate => aggregate.ClearDomainEvents())
            .Select(OutboxMessage.Create)
            .ToList();

        if (outboxMessages.Count > 0)
        {
            await _context.OutboxMessages.AddRangeAsync(outboxMessages, cancellationToken);
        }

        try
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException(ex);
        }
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }

    public void ClearTracking() => _context.ChangeTracker.Clear();
}

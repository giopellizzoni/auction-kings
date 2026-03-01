using AuctionKings.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace AuctionKings.Infrastructure.Persistence;

public sealed class AuctionDbContext : DbContext
{
    public AuctionDbContext(DbContextOptions<AuctionDbContext> options)
        : base(options) { }

    public DbSet<Auction> Auctions => Set<Auction>();
    public DbSet<Bid> Bids => Set<Bid>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuctionDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}

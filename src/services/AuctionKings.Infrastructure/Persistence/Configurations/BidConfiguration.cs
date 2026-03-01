using AuctionKings.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuctionKings.Infrastructure.Persistence.Configurations;

internal sealed class BidConfiguration : IEntityTypeConfiguration<Bid>
{
    public void Configure(EntityTypeBuilder<Bid> builder)
    {
        builder.ToTable("bids");

        builder.HasKey(x => x.Id);

        builder.ComplexProperty(
            x => x.Amount,
            money =>
            {
                money
                    .Property(m => m.Amount)
                    .HasColumnName("bid_amount")
                    .HasPrecision(18, 2)
                    .IsRequired();

                money
                    .Property(m => m.Currency)
                    .HasColumnName("bid_currency")
                    .HasMaxLength(3)
                    .IsRequired();
            }
        );

        builder.Property(x => x.AuctionId).IsRequired();
        builder.Property(x => x.BidderId).IsRequired();
        builder.Property(x => x.PlacedAt).IsRequired();
        builder.Property(x => x.Status).IsRequired();

        builder.HasIndex(x => x.AuctionId).HasDatabaseName("ix_bids_auction_id");

        builder
            .HasIndex(x => new { x.AuctionId, x.BidderId })
            .HasDatabaseName("ix_bids_auction_id_bidder_id");
    }
}

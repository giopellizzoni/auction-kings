using AuctionKings.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuctionKings.Infrastructure.Persistence.Configurations;

internal sealed class AuctionConfiguration : IEntityTypeConfiguration<Auction>
{
    public void Configure(EntityTypeBuilder<Auction> builder)
    {
        builder.ToTable("auctions");

        builder.HasKey(x => x.Id);

        builder
            .Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder
            .Property(x => x.Description)
            .HasMaxLength(2000)
            .IsRequired();

        builder.ComplexProperty(
            x => x.StartingPrice,
            money =>
            {
                money
                    .Property(m => m.Amount)
                    .HasColumnName("starting_price_amount")
                    .HasPrecision(18, 2)
                    .IsRequired();

                money
                    .Property(m => m.Currency)
                    .HasColumnName("starting_price_currency")
                    .HasMaxLength(3)
                    .IsRequired();
            }
        );

        builder.ComplexProperty(
            x => x.CurrentPrice,
            money =>
            {
                money
                    .Property(m => m.Amount)
                    .HasColumnName("current_price_amount")
                    .HasPrecision(18, 2)
                    .IsRequired();

                money
                    .Property(m => m.Currency)
                    .HasColumnName("current_price_currency")
                    .HasMaxLength(3)
                    .IsRequired();
            }
        );

        builder.Property(x => x.StartTime).IsRequired();
        builder.Property(x => x.EndTime).IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.SellerId).IsRequired();
        builder.Property(x => x.WinnerId);

        // Version is the application-level optimistic concurrency token.
        // PostgreSQL xmin shadow property below provides the DB-level check.
        builder.Property(x => x.Version).IsRequired().IsConcurrencyToken();

        // UseXminAsConcurrencyToken() was removed in Npgsql 10.
        // Map xmin as a shadow property using standard EF Core APIs.
        builder
            .Property<uint>("xmin")
            .HasColumnType("xid")
            .HasColumnName("xmin")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder
            .HasMany(x => x.Bids)
            .WithOne()
            .HasForeignKey(b => b.AuctionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Bids).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => x.Status).HasDatabaseName("ix_auctions_status");
        builder.HasIndex(x => x.EndTime).HasDatabaseName("ix_auctions_end_time");
        builder.HasIndex(x => x.SellerId).HasDatabaseName("ix_auctions_seller_id");
    }
}

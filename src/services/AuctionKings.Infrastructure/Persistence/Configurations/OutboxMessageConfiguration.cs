using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuctionKings.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type).HasMaxLength(500).IsRequired();

        builder.Property(x => x.Content).HasColumnType("jsonb").IsRequired();

        builder.Property(x => x.OccurredOn).IsRequired();

        builder.Property(x => x.ProcessedAt);

        builder.Property(x => x.RetryCount).HasDefaultValue(0).IsRequired();

        builder.Property(x => x.Failed).HasDefaultValue(false).IsRequired();

        builder
            .HasIndex(x => x.ProcessedAt)
            .HasFilter("processed_at IS NULL AND failed = false")
            .HasDatabaseName("ix_outbox_messages_unprocessed");
    }
}

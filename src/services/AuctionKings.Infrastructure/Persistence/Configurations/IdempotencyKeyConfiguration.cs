using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuctionKings.Infrastructure.Persistence.Configurations;

internal sealed class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKey>
{
    public void Configure(EntityTypeBuilder<IdempotencyKey> builder)
    {
        builder.ToTable("idempotency_keys");

        builder.HasKey(x => x.Key);

        builder.Property(x => x.Key).HasMaxLength(500).IsRequired();

        builder.Property(x => x.CreatedAt).IsRequired();

        builder.Property(x => x.ExpiresAt).IsRequired();

        builder.HasIndex(x => x.ExpiresAt).HasDatabaseName("ix_idempotency_keys_expires_at");
    }
}

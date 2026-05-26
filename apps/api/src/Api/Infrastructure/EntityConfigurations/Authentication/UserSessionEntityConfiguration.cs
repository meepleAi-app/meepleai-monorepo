using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

internal class UserSessionEntityConfiguration : IEntityTypeConfiguration<UserSessionEntity>
{
    public void Configure(EntityTypeBuilder<UserSessionEntity> builder)
    {
        builder.ToTable("user_sessions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(64);
        builder.Property(e => e.UserId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.TokenHash).IsRequired().HasMaxLength(128);
        builder.Property(e => e.UserAgent).HasMaxLength(256);
        builder.Property(e => e.IpAddress).HasMaxLength(64);
        builder.Property(e => e.DeviceFingerprint).HasMaxLength(64); // Base64 SHA256 = 32 bytes → 44 chars (64 for safety)
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.ExpiresAt).IsRequired();
        builder.Property(e => e.LastSeenAt);
        builder.Property(e => e.RevokedAt);
        builder.HasIndex(e => e.TokenHash).IsUnique();
        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => new { e.UserId, e.DeviceFingerprint }); // Issue #3677: Device tracking queries

        // SP5 Admin Security S2 — D-S2-2: dual-principal impersonation columns
        builder.Property(e => e.ImpersonatedByUserId).HasColumnName("impersonated_by_user_id");
        builder.Property(e => e.ImpersonatedUntil).HasColumnName("impersonated_until");

        // Partial index supports GetActiveImpersonationsQuery (T6) without scanning the whole
        // sessions table. PostgreSQL filtered index applies WHERE clause at write time so the
        // index only contains the (typically small) set of active impersonations.
        builder.HasIndex(e => e.ImpersonatedByUserId)
            .HasDatabaseName("ix_user_sessions_impersonated_by_user_id")
            .HasFilter("\"impersonated_by_user_id\" IS NOT NULL");
    }
}

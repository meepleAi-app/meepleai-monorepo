using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.SharedGameCatalog;

/// <summary>
/// Entity Framework configuration for UserBadge entity.
/// ISSUE-2731: Infrastructure - EF Core Migrations e Repository
/// </summary>
internal sealed class UserBadgeEntityConfiguration : IEntityTypeConfiguration<UserBadgeEntity>
{
    public void Configure(EntityTypeBuilder<UserBadgeEntity> builder)
    {
        builder.ToTable("user_badges");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(e => e.BadgeId)
            .HasColumnName("badge_id")
            .IsRequired();

        builder.Property(e => e.EarnedAt)
            .HasColumnName("earned_at")
            .IsRequired();

        builder.Property(e => e.TriggeringShareRequestId)
            .HasColumnName("triggering_share_request_id");

        builder.Property(e => e.IsDisplayed)
            .HasColumnName("is_displayed")
            .IsRequired();

        builder.Property(e => e.RevokedAt)
            .HasColumnName("revoked_at");

        builder.Property(e => e.RevocationReason)
            .HasColumnName("revocation_reason")
            .HasMaxLength(500);

        // Indexes
        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("ix_user_badges_user_id");

        builder.HasIndex(e => e.BadgeId)
            .HasDatabaseName("ix_user_badges_badge_id");

        builder.HasIndex(e => new { e.UserId, e.BadgeId })
            .IsUnique()
            .HasDatabaseName("ix_user_badges_user_badge_unique");

        builder.HasIndex(e => e.TriggeringShareRequestId)
            .HasDatabaseName("ix_user_badges_triggering_share_request_id")
            .HasFilter("triggering_share_request_id IS NOT NULL");

        builder.HasIndex(e => e.RevokedAt)
            .HasDatabaseName("ix_user_badges_revoked_at")
            .HasFilter("revoked_at IS NOT NULL");

        // Relationships
        builder.HasOne(e => e.Badge)
            .WithMany()
            .HasForeignKey(e => e.BadgeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

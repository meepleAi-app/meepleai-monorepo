using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.SharedGameCatalog;

/// <summary>
/// Entity Framework configuration for ShareRequest entity.
/// Issue #2724: CreateShareRequest Command infrastructure.
/// </summary>
internal sealed class ShareRequestEntityConfiguration : IEntityTypeConfiguration<ShareRequestEntity>
{
    public void Configure(EntityTypeBuilder<ShareRequestEntity> builder)
    {
        builder.ToTable("share_requests");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(e => e.SourceGameId)
            .HasColumnName("source_game_id")
            .IsRequired();

        builder.Property(e => e.TargetSharedGameId)
            .HasColumnName("target_shared_game_id");

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .IsRequired();

        builder.Property(e => e.StatusBeforeReview)
            .HasColumnName("status_before_review");

        builder.Property(e => e.ContributionType)
            .HasColumnName("contribution_type")
            .IsRequired();

        builder.Property(e => e.UserNotes)
            .HasColumnName("user_notes")
            .HasMaxLength(2000);

        builder.Property(e => e.AdminFeedback)
            .HasColumnName("admin_feedback")
            .HasMaxLength(2000);

        builder.Property(e => e.ReviewingAdminId)
            .HasColumnName("reviewing_admin_id");

        builder.Property(e => e.ReviewStartedAt)
            .HasColumnName("review_started_at");

        builder.Property(e => e.ReviewLockExpiresAt)
            .HasColumnName("review_lock_expires_at");

        builder.Property(e => e.ResolvedAt)
            .HasColumnName("resolved_at");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.ModifiedAt)
            .HasColumnName("modified_at");

        builder.Property(e => e.CreatedBy)
            .HasColumnName("created_by")
            .IsRequired();

        builder.Property(e => e.ModifiedBy)
            .HasColumnName("modified_by");

        builder.Property(e => e.RowVersion)
            .HasColumnName("row_version")
            .IsRowVersion();

        // Indexes
        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("ix_share_requests_user_id");

        builder.HasIndex(e => e.SourceGameId)
            .HasDatabaseName("ix_share_requests_source_game_id");

        builder.HasIndex(e => e.Status)
            .HasDatabaseName("ix_share_requests_status");

        builder.HasIndex(e => e.ReviewingAdminId)
            .HasDatabaseName("ix_share_requests_reviewing_admin_id")
            .HasFilter("reviewing_admin_id IS NOT NULL");

        builder.HasIndex(e => new { e.UserId, e.SourceGameId, e.Status })
            .HasDatabaseName("ix_share_requests_user_source_status");

        builder.HasIndex(e => e.ReviewLockExpiresAt)
            .HasDatabaseName("ix_share_requests_review_lock_expires_at")
            .HasFilter("review_lock_expires_at IS NOT NULL");

        // Relationships
        builder.HasOne(e => e.SourceGame)
            .WithMany()
            .HasForeignKey(e => e.SourceGameId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.TargetSharedGame)
            .WithMany()
            .HasForeignKey(e => e.TargetSharedGameId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(e => e.AttachedDocuments)
            .WithOne(d => d.ShareRequest)
            .HasForeignKey(d => d.ShareRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

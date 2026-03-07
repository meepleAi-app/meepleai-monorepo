using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.GameManagement;

/// <summary>
/// EF Core configuration for SessionAttachment persistence entity.
/// Issue #5360 - SessionAttachment EF Core configuration + migration.
/// </summary>
internal sealed class SessionAttachmentEntityConfiguration : IEntityTypeConfiguration<SessionAttachmentEntity>
{
    public void Configure(EntityTypeBuilder<SessionAttachmentEntity> builder)
    {
        builder.ToTable("session_attachments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(a => a.SessionId)
            .HasColumnName("session_id")
            .IsRequired();

        builder.Property(a => a.SnapshotIndex)
            .HasColumnName("snapshot_index");

        builder.Property(a => a.PlayerId)
            .HasColumnName("player_id")
            .IsRequired();

        builder.Property(a => a.AttachmentType)
            .HasColumnName("attachment_type")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(a => a.BlobUrl)
            .HasColumnName("blob_url")
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(a => a.ThumbnailUrl)
            .HasColumnName("thumbnail_url")
            .HasMaxLength(2048);

        builder.Property(a => a.Caption)
            .HasColumnName("caption")
            .HasMaxLength(200);

        builder.Property(a => a.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.FileSizeBytes)
            .HasColumnName("file_size_bytes")
            .IsRequired();

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(a => a.IsDeleted)
            .HasColumnName("is_deleted")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(a => a.DeletedAt)
            .HasColumnName("deleted_at");

        // Soft delete global filter
        builder.HasQueryFilter(a => !a.IsDeleted);

        // Indexes
        builder.HasIndex(a => a.SessionId)
            .HasDatabaseName("ix_session_attachment_session_id")
            .HasFilter("is_deleted = false");

        builder.HasIndex(a => a.PlayerId)
            .HasDatabaseName("ix_session_attachment_player_id")
            .HasFilter("is_deleted = false");

        builder.HasIndex(a => a.CreatedAt)
            .HasDatabaseName("ix_session_attachment_cleanup")
            .HasFilter("is_deleted = false");

        builder.HasIndex(a => new { a.SessionId, a.SnapshotIndex })
            .HasDatabaseName("ix_session_attachment_snapshot")
            .HasFilter("is_deleted = false AND snapshot_index IS NOT NULL");
    }
}

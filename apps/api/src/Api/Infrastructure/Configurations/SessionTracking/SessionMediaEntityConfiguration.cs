using Api.Infrastructure.Entities.SessionTracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.SessionTracking;

/// <summary>
/// EF Core configuration for SessionMediaEntity.
/// Issue #4760: SessionMedia Entity + RAG Agent Integration + Shared Chat
/// </summary>
internal sealed class SessionMediaEntityConfiguration : IEntityTypeConfiguration<SessionMediaEntity>
{
    public void Configure(EntityTypeBuilder<SessionMediaEntity> builder)
    {
        builder.ToTable("session_tracking_media");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(m => m.SessionId)
            .HasColumnName("session_id")
            .IsRequired();

        builder.Property(m => m.ParticipantId)
            .HasColumnName("participant_id")
            .IsRequired();

        builder.Property(m => m.SnapshotId)
            .HasColumnName("snapshot_id");

        builder.Property(m => m.FileId)
            .HasColumnName("file_id")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(m => m.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(m => m.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(m => m.FileSizeBytes)
            .HasColumnName("file_size_bytes")
            .IsRequired();

        builder.Property(m => m.MediaType)
            .HasColumnName("media_type")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(m => m.Caption)
            .HasColumnName("caption")
            .HasMaxLength(500);

        builder.Property(m => m.ThumbnailFileId)
            .HasColumnName("thumbnail_file_id")
            .HasMaxLength(500);

        builder.Property(m => m.TurnNumber)
            .HasColumnName("turn_number");

        builder.Property(m => m.IsSharedWithSession)
            .HasColumnName("is_shared_with_session")
            .IsRequired();

        builder.Property(m => m.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(m => m.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(m => m.IsDeleted)
            .HasColumnName("is_deleted")
            .IsRequired();

        builder.Property(m => m.DeletedAt)
            .HasColumnName("deleted_at");

        // Global query filter for soft-delete
        builder.HasQueryFilter(m => !m.IsDeleted);

        // Indexes
        builder.HasIndex(m => m.SessionId)
            .HasDatabaseName("ix_session_tracking_media_session_id");

        builder.HasIndex(m => m.SnapshotId)
            .HasDatabaseName("ix_session_tracking_media_snapshot_id");

        builder.HasIndex(m => m.ParticipantId)
            .HasDatabaseName("ix_session_tracking_media_participant_id");

        builder.HasIndex(m => new { m.SessionId, m.CreatedAt })
            .HasDatabaseName("ix_session_tracking_media_session_id_created_at");

        // Navigation properties
        builder.HasOne(m => m.Session)
            .WithMany()
            .HasForeignKey(m => m.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Participant)
            .WithMany()
            .HasForeignKey(m => m.ParticipantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

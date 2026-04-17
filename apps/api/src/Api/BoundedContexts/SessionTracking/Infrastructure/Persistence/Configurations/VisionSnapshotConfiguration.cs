using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for VisionSnapshot.
/// Uses direct domain entity mapping (no separate persistence entity).
/// Session Vision AI feature.
/// </summary>
internal sealed class VisionSnapshotConfiguration : IEntityTypeConfiguration<VisionSnapshot>
{
    public void Configure(EntityTypeBuilder<VisionSnapshot> builder)
    {
        builder.ToTable("vision_snapshots");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.SessionId)
            .HasColumnName("session_id")
            .IsRequired();

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(e => e.TurnNumber)
            .HasColumnName("turn_number")
            .IsRequired();

        builder.Property(e => e.Caption)
            .HasColumnName("caption")
            .HasMaxLength(200);

        builder.Property(e => e.ExtractedGameState)
            .HasColumnName("extracted_game_state")
            .HasColumnType("jsonb");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(e => e.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(e => e.DeletedAt)
            .HasColumnName("deleted_at");

        // Soft delete query filter
        builder.HasQueryFilter(e => !e.IsDeleted);

        // Navigation to images
        builder.HasMany(e => e.Images)
            .WithOne()
            .HasForeignKey(e => e.VisionSnapshotId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(e => e.SessionId)
            .HasDatabaseName("ix_vision_snapshots_session_id");

        builder.HasIndex(e => new { e.SessionId, e.TurnNumber })
            .HasDatabaseName("ix_vision_snapshots_session_turn");

        builder.HasIndex(e => e.IsDeleted)
            .HasDatabaseName("ix_vision_snapshots_is_deleted");
    }
}

/// <summary>
/// EF Core configuration for VisionSnapshotImage.
/// Session Vision AI feature.
/// </summary>
internal sealed class VisionSnapshotImageConfiguration : IEntityTypeConfiguration<VisionSnapshotImage>
{
    public void Configure(EntityTypeBuilder<VisionSnapshotImage> builder)
    {
        builder.ToTable("vision_snapshot_images");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.StorageKey)
            .HasColumnName("storage_key")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.MediaType)
            .HasColumnName("media_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Width)
            .HasColumnName("width")
            .IsRequired();

        builder.Property(e => e.Height)
            .HasColumnName("height")
            .IsRequired();

        builder.Property(e => e.OrderIndex)
            .HasColumnName("order_index")
            .IsRequired();

        builder.Property(e => e.VisionSnapshotId)
            .HasColumnName("vision_snapshot_id")
            .IsRequired();

        // Indexes
        builder.HasIndex(e => e.VisionSnapshotId)
            .HasDatabaseName("ix_vision_snapshot_images_snapshot_id");
    }
}

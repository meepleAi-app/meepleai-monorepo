using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.GameManagement;

/// <summary>
/// EF Core configuration for PlayRecordPhotoEntity.
/// #2436 PR-B (ADR-067): photos attached to a PlayRecord.
/// </summary>
internal class PlayRecordPhotoEntityConfiguration : IEntityTypeConfiguration<PlayRecordPhotoEntity>
{
    public void Configure(EntityTypeBuilder<PlayRecordPhotoEntity> builder)
    {
        builder.ToTable("play_record_photos");
        builder.HasKey(e => e.Id);

        // Properties — use EF default column names (PascalCase) to match record_players convention
        builder.Property(e => e.PlayRecordId).IsRequired();
        builder.Property(e => e.BlobUrl).IsRequired().HasMaxLength(1024);
        builder.Property(e => e.ThumbnailUrl).HasMaxLength(1024);
        builder.Property(e => e.FileSizeBytes).IsRequired();
        builder.Property(e => e.Sha256Hash).IsRequired().HasMaxLength(64);
        builder.Property(e => e.OcrText);
        builder.Property(e => e.OcrConfidence);
        builder.Property(e => e.Caption).HasMaxLength(500);
        builder.Property(e => e.UploadedByUserId).IsRequired();
        builder.Property(e => e.UploadedAt).IsRequired();

        // FK to play_records (cascade-delete: photo orphans removed when record is deleted)
        builder.HasOne(e => e.PlayRecord)
            .WithMany(r => r.Photos)
            .HasForeignKey(e => e.PlayRecordId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique index: same photo cannot be attached twice to the same record
        builder.HasIndex(e => new { e.PlayRecordId, e.Sha256Hash })
            .IsUnique()
            .HasDatabaseName("UX_play_record_photos_playrecord_sha256");

        // Non-unique index for FK traversal
        builder.HasIndex(e => e.PlayRecordId)
            .HasDatabaseName("IX_play_record_photos_PlayRecordId");
    }
}

using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.GameManagement;

/// <summary>
/// EF Core configuration for GameNightPhotoEntity (Issue #2724).
/// Standalone table with a cascade-delete FK to game_night_events, so photos are
/// reclaimed when their night is hard-deleted.
/// </summary>
internal class GameNightPhotoEntityConfiguration : IEntityTypeConfiguration<GameNightPhotoEntity>
{
    public void Configure(EntityTypeBuilder<GameNightPhotoEntity> builder)
    {
        builder.ToTable("game_night_photos");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.GameNightId).IsRequired();
        builder.Property(e => e.BlobUrl).IsRequired().HasMaxLength(1024);
        builder.Property(e => e.ThumbnailUrl).HasMaxLength(1024);
        builder.Property(e => e.FileSizeBytes).IsRequired();
        builder.Property(e => e.Sha256Hash).IsRequired().HasMaxLength(64);
        builder.Property(e => e.OcrText);
        builder.Property(e => e.OcrConfidence);
        builder.Property(e => e.Caption).HasMaxLength(500);
        builder.Property(e => e.UploadedByUserId).IsRequired();
        builder.Property(e => e.UploadedAt).IsRequired();

        // FK to game_night_events (cascade-delete: photos removed when the night is deleted).
        // No navigation on the principal — photos are managed by their own repository.
        builder.HasOne<GameNightEventEntity>()
            .WithMany()
            .HasForeignKey(e => e.GameNightId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique index: same image cannot be attached twice to the same night (dedup guard).
        builder.HasIndex(e => new { e.GameNightId, e.Sha256Hash })
            .IsUnique()
            .HasDatabaseName("UX_game_night_photos_gamenight_sha256");

        // Non-unique index for FK traversal / gallery listing.
        builder.HasIndex(e => e.GameNightId)
            .HasDatabaseName("IX_game_night_photos_GameNightId");
    }
}

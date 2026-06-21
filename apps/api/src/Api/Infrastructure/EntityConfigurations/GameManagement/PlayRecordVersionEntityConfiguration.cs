using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.GameManagement;

/// <summary>
/// EF Core configuration for PlayRecordVersionEntity.
/// #2437-3: version history + restore for play records.
/// </summary>
internal class PlayRecordVersionEntityConfiguration : IEntityTypeConfiguration<PlayRecordVersionEntity>
{
    public void Configure(EntityTypeBuilder<PlayRecordVersionEntity> builder)
    {
        builder.ToTable("play_record_versions");
        builder.HasKey(e => e.Id);

        // Use EF default column names (PascalCase) — matches the play_record_photos convention.
        builder.Property(e => e.Id).IsRequired();
        builder.Property(e => e.PlayRecordId).IsRequired();
        builder.Property(e => e.VersionNumber).IsRequired();
        builder.Property(e => e.SessionDate).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(2000);
        builder.Property(e => e.Location).HasMaxLength(255);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedByUserId).IsRequired();

        // FK to play_records (cascade-delete: version history removed when record is deleted)
        builder.HasOne(e => e.PlayRecord)
            .WithMany()
            .HasForeignKey(e => e.PlayRecordId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        // Unique index: enforces (PlayRecordId, VersionNumber) uniqueness.
        builder.HasIndex(e => new { e.PlayRecordId, e.VersionNumber })
            .IsUnique()
            .HasDatabaseName("UX_play_record_versions_record_version");

        // Sort index for the history query (CreatedAt DESC per record).
        builder.HasIndex(e => new { e.PlayRecordId, e.CreatedAt })
            .HasDatabaseName("IX_play_record_versions_record_createdat");
    }
}

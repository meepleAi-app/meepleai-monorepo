using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.GameManagement;

/// <summary>
/// EF Core configuration for PlayRecordEntity.
/// Issue #3888: Play record tracking system.
/// </summary>
internal class PlayRecordEntityConfiguration : IEntityTypeConfiguration<PlayRecordEntity>
{
    public void Configure(EntityTypeBuilder<PlayRecordEntity> builder)
    {
        builder.ToTable("play_records");
        builder.HasKey(e => e.Id);

        // Game Association (Optional FK)
        builder.Property(e => e.GameId).IsRequired(false);
        builder.Property(e => e.GameName).IsRequired().HasMaxLength(255);
        builder.HasOne(e => e.Game)
            .WithMany()
            .HasForeignKey(e => e.GameId)
            .OnDelete(DeleteBehavior.SetNull);

        // Ownership
        builder.Property(e => e.CreatedByUserId).IsRequired();
        builder.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);  // Prevent user deletion with play history
        builder.Property(e => e.Visibility).IsRequired();
        builder.Property(e => e.GroupId).IsRequired(false);

        // Session Metadata
        builder.Property(e => e.SessionDate).IsRequired();
        builder.Property(e => e.StartTime).IsRequired(false);
        builder.Property(e => e.EndTime).IsRequired(false);
        builder.Property(e => e.Duration).IsRequired(false);
        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(2000);
        builder.Property(e => e.Location).HasMaxLength(255);

        // Scoring Configuration (JSON)
        builder.Property(e => e.ScoringConfigJson)
            .IsRequired()
            .HasColumnType("jsonb");

        // Audit
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();

        // Indexes
        builder.HasIndex(e => e.GameId)
            .HasDatabaseName("IX_PlayRecords_GameId")
            .HasFilter("\"GameId\" IS NOT NULL");
        builder.HasIndex(e => e.CreatedByUserId)
            .HasDatabaseName("IX_PlayRecords_CreatedByUserId");
        builder.HasIndex(e => e.SessionDate)
            .HasDatabaseName("IX_PlayRecords_SessionDate")
            .IsDescending();
        builder.HasIndex(e => e.Status)
            .HasDatabaseName("IX_PlayRecords_Status");

        // Relationships
        builder.HasMany(e => e.Players)
            .WithOne(p => p.PlayRecord)
            .HasForeignKey(p => p.PlayRecordId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

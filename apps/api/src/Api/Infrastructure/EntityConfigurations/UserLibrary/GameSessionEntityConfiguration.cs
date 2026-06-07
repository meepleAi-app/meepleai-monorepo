using Api.Infrastructure.Entities.UserLibrary;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.UserLibrary;

/// <summary>
/// EF Core configuration for GameSessionEntity.
/// Maps to "game_sessions" table with optimized indexes for queries.
/// </summary>
internal class GameSessionEntityConfiguration : IEntityTypeConfiguration<UserGameSessionEntity>
{
    public void Configure(EntityTypeBuilder<UserGameSessionEntity> builder)
    {
        builder.ToTable("game_sessions");

        builder.HasKey(e => e.Id);

        // Foreign key to UserLibraryEntry
        builder.Property(e => e.UserLibraryEntryId).IsRequired();
        builder.HasOne(e => e.UserLibraryEntry)
            .WithMany()
            .HasForeignKey(e => e.UserLibraryEntryId)
            .OnDelete(DeleteBehavior.Cascade); // Delete sessions when library entry is deleted

        // Session properties
        builder.Property(e => e.PlayedAt)
            .IsRequired();

        builder.Property(e => e.DurationMinutes)
            .HasColumnName("duration_minutes")
            .IsRequired();

        builder.Property(e => e.DidWin)
            .IsRequired(false);

        builder.Property(e => e.Players)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(e => e.Notes)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.UpdatedAt)
            .IsRequired(false);

        // Indexes for performance
        builder.HasIndex(e => e.UserLibraryEntryId)
            .HasDatabaseName("ix_game_sessions_user_library_entry_id");

        builder.HasIndex(e => e.PlayedAt)
            .HasDatabaseName("ix_game_sessions_played_at");

        // Composite index for recent sessions query
        builder.HasIndex(e => new { e.UserLibraryEntryId, e.PlayedAt })
            .HasDatabaseName("ix_game_sessions_entry_played");

        // Issue #1929 Task C Macro 4 (DEC-B-8, DEC-C-10 REVISION) — TestRunId partial index.
        // Null for production entities; non-null only when seeded via Testing BC.
        builder.Property(e => e.TestRunId)
            .HasMaxLength(64)
            .IsRequired(false);

        builder.HasIndex(e => e.TestRunId)
            .HasDatabaseName("ix_game_sessions_test_run_id")
            .HasFilter("\"test_run_id\" IS NOT NULL");

        // Check constraints for domain validation
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("chk_game_sessions_duration",
                "duration_minutes > 0");
        });
    }
}

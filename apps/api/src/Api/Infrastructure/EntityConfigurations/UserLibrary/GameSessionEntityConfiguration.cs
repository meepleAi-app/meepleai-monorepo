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

        // Check constraints for domain validation
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("chk_game_sessions_duration",
                "duration_minutes > 0");
        });
    }
}

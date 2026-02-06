using Api.Infrastructure.Entities.UserLibrary;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.UserLibrary;

/// <summary>
/// EF Core configuration for UserLibraryEntryEntity.
/// Issue #3662: Updated to support both SharedGame and PrivateGame references.
/// </summary>
internal class UserLibraryEntryEntityConfiguration : IEntityTypeConfiguration<UserLibraryEntryEntity>
{
    public void Configure(EntityTypeBuilder<UserLibraryEntryEntity> builder)
    {
        builder.ToTable("user_library_entries");

        builder.HasKey(e => e.Id);

        // Ignore the obsolete GameId property - use SharedGameId instead
        builder.Ignore(e => e.GameId);

        // Issue #3662: Unique constraint for SharedGame entries
        // User can only have one entry per shared game
        builder.HasIndex(e => new { e.UserId, e.SharedGameId })
            .IsUnique()
            .HasDatabaseName("IX_UserLibraryEntries_UserId_SharedGameId")
            .HasFilter("shared_game_id IS NOT NULL");

        // Issue #3662: Unique constraint for PrivateGame entries
        // User can only have one entry per private game
        builder.HasIndex(e => new { e.UserId, e.PrivateGameId })
            .IsUnique()
            .HasDatabaseName("IX_UserLibraryEntries_UserId_PrivateGameId")
            .HasFilter("private_game_id IS NOT NULL");

        // Index for querying user's library
        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("IX_UserLibraryEntries_UserId");

        // Index for querying by added date (for sorting)
        builder.HasIndex(e => e.AddedAt)
            .HasDatabaseName("IX_UserLibraryEntries_AddedAt");

        // Index for private PDF association
        builder.HasIndex(e => e.PrivatePdfId)
            .HasDatabaseName("IX_UserLibraryEntries_PrivatePdfId");

        // Issue #3662: SharedGameId and PrivateGameId column properties
        builder.Property(e => e.SharedGameId)
            .HasColumnName("shared_game_id")
            .IsRequired(false);

        builder.Property(e => e.PrivateGameId)
            .HasColumnName("private_game_id")
            .IsRequired(false);

        // Properties
        builder.Property(e => e.AddedAt)
            .IsRequired();

        builder.Property(e => e.Notes)
            .HasMaxLength(500);

        builder.Property(e => e.IsFavorite)
            .HasDefaultValue(false);

        // Game state properties
        builder.Property(e => e.CurrentState)
            .IsRequired()
            .HasDefaultValue(0); // Nuovo = 0

        builder.Property(e => e.StateChangedAt)
            .IsRequired(false);

        builder.Property(e => e.StateNotes)
            .HasMaxLength(500)
            .IsRequired(false);

        // Game statistics properties
        builder.Property(e => e.TimesPlayed)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.LastPlayed)
            .IsRequired(false);

        builder.Property(e => e.WinRate)
            .HasColumnType("decimal(5,2)")
            .IsRequired(false);

        builder.Property(e => e.AvgDuration)
            .IsRequired(false);

        builder.Property(e => e.CompetitiveSessions)
            .IsRequired()
            .HasDefaultValue(0);

        // Optimistic concurrency control
        builder.Property(e => e.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        // Custom agent configuration (JSONB column for PostgreSQL)
        builder.Property(e => e.CustomAgentConfigJson)
            .HasColumnType("jsonb")
            .IsRequired(false);

        // Custom PDF metadata
        builder.Property(e => e.CustomPdfUrl)
            .HasMaxLength(2048)
            .IsRequired(false);

        builder.Property(e => e.CustomPdfUploadedAt)
            .IsRequired(false);

        builder.Property(e => e.CustomPdfFileSizeBytes)
            .IsRequired(false);

        builder.Property(e => e.CustomPdfOriginalFileName)
            .HasMaxLength(255)
            .IsRequired(false);

        // Indexes for custom features
        builder.HasIndex(e => e.CustomAgentConfigJson)
            .HasDatabaseName("IX_UserLibraryEntries_CustomAgentConfigJson")
            .HasMethod("gin"); // GIN index for JSONB queries in PostgreSQL

        // Relationships
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Issue #3662: Relationship to SharedGameCatalog (now nullable)
        builder.HasOne(e => e.SharedGame)
            .WithMany()
            .HasForeignKey(e => e.SharedGameId)
            .OnDelete(DeleteBehavior.Cascade);

        // Issue #3662: Relationship to PrivateGame
        builder.HasOne(e => e.PrivateGame)
            .WithMany(pg => pg.LibraryEntries)
            .HasForeignKey(e => e.PrivateGameId)
            .OnDelete(DeleteBehavior.SetNull);

        // Relationship to PdfDocument (private PDF association)
        builder.HasOne(e => e.PdfDocument)
            .WithMany()
            .HasForeignKey(e => e.PrivatePdfId)
            .OnDelete(DeleteBehavior.SetNull);

        // Child entity collections
        builder.HasMany(e => e.Sessions)
            .WithOne(s => s.UserLibraryEntry)
            .HasForeignKey(s => s.UserLibraryEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Checklist)
            .WithOne(c => c.UserLibraryEntry)
            .HasForeignKey(c => c.UserLibraryEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Labels)
            .WithOne(l => l.UserLibraryEntry)
            .HasForeignKey(l => l.UserLibraryEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for new query scenarios
        builder.HasIndex(e => e.CurrentState)
            .HasDatabaseName("IX_UserLibraryEntries_CurrentState");

        builder.HasIndex(e => new { e.UserId, e.CurrentState })
            .HasDatabaseName("IX_UserLibraryEntries_UserId_CurrentState");

        builder.HasIndex(e => e.LastPlayed)
            .HasDatabaseName("IX_UserLibraryEntries_LastPlayed");

        // Issue #3662: XOR constraint - Either SharedGameId OR PrivateGameId must be set, but not both
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_UserLibraryEntry_GameSource",
                "(shared_game_id IS NOT NULL AND private_game_id IS NULL) OR " +
                "(shared_game_id IS NULL AND private_game_id IS NOT NULL)");
        });
    }
}

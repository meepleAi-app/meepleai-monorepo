using Api.Infrastructure.Entities.UserLibrary;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.UserLibrary;

/// <summary>
/// EF Core configuration for UserLibraryEntryEntity.
/// </summary>
internal class UserLibraryEntryEntityConfiguration : IEntityTypeConfiguration<UserLibraryEntryEntity>
{
    public void Configure(EntityTypeBuilder<UserLibraryEntryEntity> builder)
    {
        builder.ToTable("user_library_entries");

        builder.HasKey(e => e.Id);

        // Unique constraint: user can only have one entry per game
        builder.HasIndex(e => new { e.UserId, e.GameId })
            .IsUnique()
            .HasDatabaseName("IX_UserLibraryEntries_UserId_GameId");

        // Index for querying user's library
        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("IX_UserLibraryEntries_UserId");

        // Index for querying by added date (for sorting)
        builder.HasIndex(e => e.AddedAt)
            .HasDatabaseName("IX_UserLibraryEntries_AddedAt");

        // Index for private PDF association
        builder.HasIndex(e => e.PrivatePdfId)
            .HasDatabaseName("IX_UserLibraryEntries_PrivatePdfId");

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

        // Relationship to SharedGameCatalog (not legacy games table)
        builder.HasOne(e => e.SharedGame)
            .WithMany()
            .HasForeignKey(e => e.GameId)
            .OnDelete(DeleteBehavior.Cascade);

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

        // Indexes for new query scenarios
        builder.HasIndex(e => e.CurrentState)
            .HasDatabaseName("IX_UserLibraryEntries_CurrentState");

        builder.HasIndex(e => new { e.UserId, e.CurrentState })
            .HasDatabaseName("IX_UserLibraryEntries_UserId_CurrentState");

        builder.HasIndex(e => e.LastPlayed)
            .HasDatabaseName("IX_UserLibraryEntries_LastPlayed");
    }
}

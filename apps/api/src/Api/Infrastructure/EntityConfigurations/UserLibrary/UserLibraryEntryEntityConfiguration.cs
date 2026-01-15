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

        // Properties
        builder.Property(e => e.AddedAt)
            .IsRequired();

        builder.Property(e => e.Notes)
            .HasMaxLength(500);

        builder.Property(e => e.IsFavorite)
            .HasDefaultValue(false);

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
    }
}

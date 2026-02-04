using Api.Infrastructure.Entities.UserLibrary;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.UserLibrary;

/// <summary>
/// EF Core configuration for UserGameLabelEntity.
/// Junction table linking UserLibraryEntry to GameLabel.
/// </summary>
internal class UserGameLabelEntityConfiguration : IEntityTypeConfiguration<UserGameLabelEntity>
{
    public void Configure(EntityTypeBuilder<UserGameLabelEntity> builder)
    {
        builder.ToTable("user_game_labels");

        builder.HasKey(e => e.Id);

        // Properties
        builder.Property(e => e.UserLibraryEntryId)
            .IsRequired();

        builder.Property(e => e.LabelId)
            .IsRequired();

        builder.Property(e => e.AssignedAt)
            .IsRequired();

        // Unique constraint: a label can only be assigned once to a game
        builder.HasIndex(e => new { e.UserLibraryEntryId, e.LabelId })
            .IsUnique()
            .HasDatabaseName("IX_UserGameLabels_EntryId_LabelId");

        // Index for querying labels by entry
        builder.HasIndex(e => e.UserLibraryEntryId)
            .HasDatabaseName("IX_UserGameLabels_UserLibraryEntryId");

        // Index for querying entries by label
        builder.HasIndex(e => e.LabelId)
            .HasDatabaseName("IX_UserGameLabels_LabelId");

        // Relationships are configured in parent entity configurations
        // (UserLibraryEntryEntityConfiguration and GameLabelEntityConfiguration)
    }
}

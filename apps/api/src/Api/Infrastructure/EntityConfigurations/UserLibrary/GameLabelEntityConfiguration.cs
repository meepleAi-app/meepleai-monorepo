using Api.Infrastructure.Entities.UserLibrary;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.UserLibrary;

/// <summary>
/// EF Core configuration for GameLabelEntity.
/// </summary>
internal class GameLabelEntityConfiguration : IEntityTypeConfiguration<GameLabelEntity>
{
    public void Configure(EntityTypeBuilder<GameLabelEntity> builder)
    {
        builder.ToTable("game_labels");

        builder.HasKey(e => e.Id);

        // Properties
        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.Color)
            .IsRequired()
            .HasMaxLength(7);

        builder.Property(e => e.IsPredefined)
            .HasColumnName("is_predefined")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.UserId)
            .IsRequired(false);

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        // Indexes
        // Index for querying user's custom labels
        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("IX_GameLabels_UserId");

        // Index for querying predefined labels
        builder.HasIndex(e => e.IsPredefined)
            .HasDatabaseName("IX_GameLabels_IsPredefined");

        // Unique index: user can't have duplicate label names
        builder.HasIndex(e => new { e.UserId, e.Name })
            .IsUnique()
            .HasDatabaseName("IX_GameLabels_UserId_Name")
            .HasFilter("\"IsPredefined\" = false"); // Only for custom labels

        // Unique index: predefined labels must have unique names
        builder.HasIndex(e => e.Name)
            .IsUnique()
            .HasDatabaseName("IX_GameLabels_Name_Predefined")
            .HasFilter("\"IsPredefined\" = true");

        // Relationships
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.GameLabels)
            .WithOne(gl => gl.Label)
            .HasForeignKey(gl => gl.LabelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

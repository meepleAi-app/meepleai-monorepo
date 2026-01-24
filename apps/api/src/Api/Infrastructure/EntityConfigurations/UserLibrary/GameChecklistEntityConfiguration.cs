using Api.Infrastructure.Entities.UserLibrary;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.UserLibrary;

/// <summary>
/// EF Core configuration for GameChecklistEntity.
/// Maps to "game_checklists" table with optimized indexes for ordering.
/// </summary>
internal class GameChecklistEntityConfiguration : IEntityTypeConfiguration<UserGameChecklistEntity>
{
    public void Configure(EntityTypeBuilder<UserGameChecklistEntity> builder)
    {
        builder.ToTable("game_checklists");

        builder.HasKey(e => e.Id);

        // Foreign key to UserLibraryEntry
        builder.Property(e => e.UserLibraryEntryId).IsRequired();
        builder.HasOne(e => e.UserLibraryEntry)
            .WithMany()
            .HasForeignKey(e => e.UserLibraryEntryId)
            .OnDelete(DeleteBehavior.Cascade); // Delete checklist when library entry is deleted

        // Checklist properties
        builder.Property(e => e.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.Order)
            .IsRequired();

        builder.Property(e => e.IsCompleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.AdditionalInfo)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.UpdatedAt)
            .IsRequired(false);

        // Indexes for performance
        builder.HasIndex(e => e.UserLibraryEntryId)
            .HasDatabaseName("ix_game_checklists_user_library_entry_id");

        // Composite index for ordered checklist queries
        builder.HasIndex(e => new { e.UserLibraryEntryId, e.Order })
            .HasDatabaseName("ix_game_checklists_entry_order");

        // Check constraints for domain validation
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("chk_game_checklists_order",
                "\"order\" >= 0");
        });
    }
}

using Api.Infrastructure.Entities.UserLibrary;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.UserLibrary;

/// <summary>
/// EF Core configuration for UserCollectionEntryEntity.
/// Issue #4263: Phase 2 - Generic UserCollection System
/// </summary>
internal class UserCollectionEntryEntityConfiguration : IEntityTypeConfiguration<UserCollectionEntryEntity>
{
    public void Configure(EntityTypeBuilder<UserCollectionEntryEntity> builder)
    {
        builder.ToTable("user_collection_entries");

        builder.HasKey(e => e.Id);

        // Properties
        builder.Property(e => e.UserId)
            .IsRequired();

        builder.Property(e => e.EntityType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.EntityId)
            .IsRequired();

        builder.Property(e => e.IsFavorite)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.Notes)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(e => e.MetadataJson)
            .HasColumnType("jsonb")
            .IsRequired(false);

        builder.Property(e => e.AddedAt)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .IsRequired(false);

        // Indexes
        // User's collection query
        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("IX_UserCollectionEntries_UserId");

        // Entity type and ID lookup
        builder.HasIndex(e => new { e.EntityType, e.EntityId })
            .HasDatabaseName("IX_UserCollectionEntries_EntityType_EntityId");

        // User favorites query (partial index)
        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("IX_UserCollectionEntries_UserId_Favorites")
            .HasFilter("[IsFavorite] = 1");

        // Unique constraint: one entry per user per entity
        builder.HasIndex(e => new { e.UserId, e.EntityType, e.EntityId })
            .IsUnique()
            .HasDatabaseName("IX_UserCollectionEntries_UserId_EntityType_EntityId");

        // CHECK constraint: EntityType must be one of the allowed values
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_UserCollectionEntries_EntityType",
            "[EntityType] IN ('Player', 'Event', 'Session', 'Agent', 'Document', 'ChatSession')"));

        // Relationships
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

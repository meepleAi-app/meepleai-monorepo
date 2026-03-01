using Api.BoundedContexts.EntityRelationships.Domain.Aggregates;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.EntityRelationships.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for EntityLink aggregate (Issue #5132).
/// Direct domain-entity mapping — no persistence entity required.
/// </summary>
internal sealed class EntityLinkEntityConfiguration : IEntityTypeConfiguration<EntityLink>
{
    public void Configure(EntityTypeBuilder<EntityLink> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("entity_links", schema: "entity_relationships");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .IsRequired()
            .ValueGeneratedNever();

        builder.Property(x => x.SourceEntityType)
            .HasColumnName("source_entity_type")
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.SourceEntityId)
            .HasColumnName("source_entity_id")
            .IsRequired();

        builder.Property(x => x.TargetEntityType)
            .HasColumnName("target_entity_type")
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.TargetEntityId)
            .HasColumnName("target_entity_id")
            .IsRequired();

        builder.Property(x => x.LinkType)
            .HasColumnName("link_type")
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.IsBidirectional)
            .HasColumnName("is_bidirectional")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.Scope)
            .HasColumnName("scope")
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.OwnerUserId)
            .HasColumnName("owner_user_id")
            .IsRequired();

        builder.Property(x => x.Metadata)
            .HasColumnName("metadata")
            .IsRequired(false)
            .HasColumnType("jsonb");

        builder.Property(x => x.IsAdminApproved)
            .HasColumnName("is_admin_approved")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.IsBggImported)
            .HasColumnName("is_bgg_imported")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.IsDeleted)
            .HasColumnName("is_deleted")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.DeletedAt)
            .HasColumnName("deleted_at")
            .IsRequired(false);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // BR-08: Unique per (sourceEntityType, sourceEntityId, targetEntityType, targetEntityId, linkType)
        // Only enforced on non-deleted rows.
        builder.HasIndex(x => new
        {
            x.SourceEntityType,
            x.SourceEntityId,
            x.TargetEntityType,
            x.TargetEntityId,
            x.LinkType
        })
            .IsUnique()
            .HasDatabaseName("uq_entity_links_source_target_type")
            .HasFilter("is_deleted = false");

        // Query performance: fetch links for a given source entity
        builder.HasIndex(x => new { x.SourceEntityType, x.SourceEntityId, x.IsDeleted })
            .HasDatabaseName("ix_entity_links_source")
            .HasFilter("is_deleted = false");

        // Query performance: reverse lookup (bilateral links)
        builder.HasIndex(x => new { x.TargetEntityType, x.TargetEntityId, x.IsDeleted })
            .HasDatabaseName("ix_entity_links_target")
            .HasFilter("is_deleted = false");

        // Query performance: fetch all links owned by a user
        builder.HasIndex(x => new { x.OwnerUserId, x.IsDeleted })
            .HasDatabaseName("ix_entity_links_owner")
            .HasFilter("is_deleted = false");

        // Global query filter — exclude soft-deleted links from all queries
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

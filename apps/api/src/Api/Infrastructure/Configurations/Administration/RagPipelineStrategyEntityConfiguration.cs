using Api.BoundedContexts.Administration.Domain.Aggregates.RagPipelineStrategy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.Administration;

/// <summary>
/// EF Core configuration for RagPipelineStrategy entity.
/// Issue #3464: Save/load/export for custom strategies.
/// </summary>
internal sealed class RagPipelineStrategyEntityConfiguration : IEntityTypeConfiguration<RagPipelineStrategy>
{
    public void Configure(EntityTypeBuilder<RagPipelineStrategy> builder)
    {
        builder.ToTable("rag_pipeline_strategies");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id");

        builder.Property(s => s.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.Description)
            .HasColumnName("description")
            .HasMaxLength(2000);

        builder.Property(s => s.Version)
            .HasColumnName("version")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.NodesJson)
            .HasColumnName("nodes_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(s => s.EdgesJson)
            .HasColumnName("edges_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(s => s.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .IsRequired();

        builder.Property(s => s.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(s => s.IsTemplate)
            .HasColumnName("is_template")
            .HasDefaultValue(false);

        builder.Property(s => s.TemplateCategory)
            .HasColumnName("template_category")
            .HasMaxLength(100);

        builder.Property(s => s.TagsJson)
            .HasColumnName("tags_json")
            .HasColumnType("jsonb")
            .HasDefaultValue("[]");

        builder.Property(s => s.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(s => s.DeletedAt)
            .HasColumnName("deleted_at");

        // Timestamps
        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Indexes
        builder.HasIndex(s => s.CreatedByUserId)
            .HasDatabaseName("ix_rag_pipeline_strategies_created_by_user_id");

        builder.HasIndex(s => s.IsTemplate)
            .HasDatabaseName("ix_rag_pipeline_strategies_is_template")
            .HasFilter("is_template = true");

        builder.HasIndex(s => new { s.Name, s.CreatedByUserId })
            .HasDatabaseName("ix_rag_pipeline_strategies_name_user")
            .IsUnique()
            .HasFilter("is_deleted = false");

        // Global query filter for soft delete
        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}

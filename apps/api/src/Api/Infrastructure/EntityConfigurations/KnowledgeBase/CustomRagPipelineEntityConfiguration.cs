using Api.Infrastructure.Entities.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.KnowledgeBase;

/// <summary>
/// EF Core configuration for CustomRagPipelineEntity.
/// Issue #3453: Visual RAG Strategy Builder - Database schema.
/// </summary>
internal sealed class CustomRagPipelineEntityConfiguration : IEntityTypeConfiguration<CustomRagPipelineEntity>
{
    public void Configure(EntityTypeBuilder<CustomRagPipelineEntity> builder)
    {
        builder.ToTable("custom_rag_pipelines");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Description)
            .HasMaxLength(1000);

        builder.Property(e => e.PipelineJson)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(e => e.CreatedBy)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(e => e.UpdatedAt);

        builder.Property(e => e.IsPublished)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.Tags)
            .HasColumnType("text[]")
            .IsRequired();

        builder.Property(e => e.IsTemplate)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.AccessTier)
            .HasMaxLength(50);

        // Indexes for performance
        builder.HasIndex(e => e.CreatedBy)
            .HasDatabaseName("ix_custom_rag_pipelines_created_by");

        builder.HasIndex(e => e.IsPublished)
            .HasDatabaseName("ix_custom_rag_pipelines_is_published");

        builder.HasIndex(e => e.IsTemplate)
            .HasDatabaseName("ix_custom_rag_pipelines_is_template");

        builder.HasIndex(e => e.Tags)
            .HasDatabaseName("ix_custom_rag_pipelines_tags")
            .HasMethod("gin"); // GIN index for array operations
    }
}

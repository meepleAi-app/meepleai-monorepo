using Api.Infrastructure.Entities.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.KnowledgeBase;

/// <summary>
/// EF Core configuration for AgentTypologyEntity.
/// Issue #3175
/// </summary>
internal class AgentTypologyEntityConfiguration : IEntityTypeConfiguration<AgentTypologyEntity>
{
    public void Configure(EntityTypeBuilder<AgentTypologyEntity> builder)
    {
        builder.ToTable("agent_typologies");

        // Primary key
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        // Properties
        builder.Property(e => e.Name)
            .HasColumnName("name")
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Description)
            .HasColumnName("description")
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.BasePrompt)
            .HasColumnName("base_prompt")
            .IsRequired()
            .HasMaxLength(5000);

        builder.Property(e => e.DefaultStrategyJson)
            .HasColumnName("default_strategy_json")
            .IsRequired()
            .HasColumnType("jsonb"); // PostgreSQL JSONB for efficient querying

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .IsRequired();

        // Audit fields
        builder.Property(e => e.CreatedBy)
            .HasColumnName("created_by")
            .IsRequired();

        builder.Property(e => e.ApprovedBy)
            .HasColumnName("approved_by");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.ApprovedAt)
            .HasColumnName("approved_at");

        // Soft delete
        builder.Property(e => e.IsDeleted)
            .HasColumnName("is_deleted")
            .IsRequired()
            .HasDefaultValue(false);

        // Query filter for soft delete
        builder.HasQueryFilter(e => !e.IsDeleted);

        // Indexes
        builder.HasIndex(e => e.Name)
            .HasDatabaseName("ix_agent_typologies_name")
            .IsUnique()
            .HasFilter("is_deleted = false"); // Unique constraint only for non-deleted

        builder.HasIndex(e => e.Status)
            .HasDatabaseName("ix_agent_typologies_status");

        builder.HasIndex(e => e.CreatedBy)
            .HasDatabaseName("ix_agent_typologies_created_by");

        // Relationships (cascade delete to typology prompt templates)
        builder.HasMany(e => e.TypologyPromptTemplates)
            .WithOne(p => p.Typology)
            .HasForeignKey(p => p.TypologyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

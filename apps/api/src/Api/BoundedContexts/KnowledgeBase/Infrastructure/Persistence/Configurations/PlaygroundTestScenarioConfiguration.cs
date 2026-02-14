using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for PlaygroundTestScenario entity.
/// Issue #4396: PlaygroundTestScenario Entity + CRUD
/// </summary>
public sealed class PlaygroundTestScenarioConfiguration : IEntityTypeConfiguration<PlaygroundTestScenario>
{
    public void Configure(EntityTypeBuilder<PlaygroundTestScenario> builder)
    {
        builder.ToTable("playground_test_scenarios", "knowledge_base");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(p => p.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(p => p.Name).IsUnique();

        builder.Property(p => p.Description)
            .HasColumnName("description")
            .HasMaxLength(2000)
            .IsRequired();

        // Category enum stored as int
        builder.Property(p => p.Category)
            .HasColumnName("category")
            .IsRequired();

        // Messages stored as JSONB (private backing field)
        builder.Ignore(p => p.Messages);
        builder.Property<string>("_messagesJson")
            .HasColumnName("messages")
            .HasColumnType("jsonb")
            .IsRequired();

        // Tags stored as JSONB (private backing field)
        builder.Ignore(p => p.Tags);
        builder.Property<string>("_tagsJson")
            .HasColumnName("tags")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(p => p.ExpectedOutcome)
            .HasColumnName("expected_outcome")
            .HasMaxLength(2000);

        builder.Property(p => p.AgentDefinitionId)
            .HasColumnName("agent_definition_id");

        builder.Property(p => p.CreatedBy)
            .HasColumnName("created_by")
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(p => p.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        // Indexes for search performance
        builder.HasIndex(p => p.IsActive);
        builder.HasIndex(p => p.Category);
        builder.HasIndex(p => p.AgentDefinitionId);
        builder.HasIndex(p => p.CreatedBy);
        builder.HasIndex(p => p.CreatedAt);
    }
}

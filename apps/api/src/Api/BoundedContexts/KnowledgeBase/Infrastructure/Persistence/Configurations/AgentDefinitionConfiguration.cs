using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for AgentDefinition entity (Issue #3808, Epic #3687)
/// </summary>
public sealed class AgentDefinitionConfiguration : IEntityTypeConfiguration<AgentDefinition>
{
    public void Configure(EntityTypeBuilder<AgentDefinition> builder)
    {
        builder.ToTable("agent_definitions", "knowledge_base");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(a => a.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(a => a.Name).IsUnique();

        builder.Property(a => a.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        // AgentDefinitionConfig value object (owned)
        builder.OwnsOne(a => a.Config, config =>
        {
            config.Property(c => c.Model).HasColumnName("model").HasMaxLength(200).IsRequired();
            config.Property(c => c.MaxTokens).HasColumnName("max_tokens").IsRequired();
            config.Property(c => c.Temperature).HasColumnName("temperature").IsRequired();
        });

        // Prompts (JSON column - stored as string, handled by entity)
        builder.Ignore(a => a.Prompts);
        builder.Property<string>("_promptsJson")
            .HasColumnName("prompts")
            .HasColumnType("jsonb")
            .IsRequired();

        // Tools (JSON column - stored as string, handled by entity)
        builder.Ignore(a => a.Tools);
        builder.Property<string>("_toolsJson")
            .HasColumnName("tools")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(a => a.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");

        // Indexes for search performance
        builder.HasIndex(a => a.IsActive);
        builder.HasIndex(a => a.CreatedAt);
    }
}

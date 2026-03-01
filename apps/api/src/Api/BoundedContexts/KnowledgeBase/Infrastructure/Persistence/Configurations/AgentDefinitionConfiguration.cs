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

        // AgentType (Issue #3708) - stored as value + description, computed property ignored
        builder.Ignore(a => a.Type);
        builder.Property<string>("_typeValue")
            .HasColumnName("type_value")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property<string>("_typeDescription")
            .HasColumnName("type_description")
            .HasMaxLength(200)
            .IsRequired();

        // AgentDefinitionConfig value object (owned)
        builder.OwnsOne(a => a.Config, config =>
        {
            config.Property(c => c.Model).HasColumnName("model").HasMaxLength(200).IsRequired();
            config.Property(c => c.MaxTokens).HasColumnName("max_tokens").IsRequired();
            config.Property(c => c.Temperature).HasColumnName("temperature").IsRequired();
        });

        // AgentStrategy (Issue #3708) - stored as JSONB, computed property ignored
        builder.Ignore(a => a.Strategy);
        builder.Property<string>("_strategyJson")
            .HasColumnName("strategy")
            .HasColumnType("jsonb")
            .IsRequired();

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

        // KbCardIds (Issue #4932) - stored as JSONB array of Guid
        builder.Ignore(a => a.KbCardIds);
        builder.Property<string>("_kbCardIdsJson")
            .HasColumnName("kb_card_ids")
            .HasColumnType("jsonb")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(a => a.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");

        // Indexes for search performance
        builder.HasIndex(a => a.IsActive);
        builder.HasIndex(a => a.CreatedAt);
        builder.HasIndex("_typeValue").HasDatabaseName("ix_agent_definitions_type_value");
    }
}

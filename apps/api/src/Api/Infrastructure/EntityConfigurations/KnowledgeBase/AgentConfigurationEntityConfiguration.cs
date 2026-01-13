using Api.Infrastructure.Entities.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.KnowledgeBase;

/// <summary>
/// EF Core configuration for AgentConfigurationEntity.
/// Issue #2391 Sprint 2
/// </summary>
internal class AgentConfigurationEntityConfiguration : IEntityTypeConfiguration<AgentConfigurationEntity>
{
    public void Configure(EntityTypeBuilder<AgentConfigurationEntity> builder)
    {
        builder.ToTable("agent_configurations", t =>
        {
            t.HasCheckConstraint(
                "ck_agent_configurations_temperature",
                "temperature >= 0.0 AND temperature <= 2.0");

            t.HasCheckConstraint(
                "ck_agent_configurations_max_tokens",
                "max_tokens > 0 AND max_tokens <= 32000");
        });

        // Primary key
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        // Foreign key to Agent
        builder.Property(e => e.AgentId)
            .HasColumnName("agent_id")
            .IsRequired();

        // LLM Configuration
        builder.Property(e => e.LlmProvider)
            .HasColumnName("llm_provider")
            .IsRequired();

        builder.Property(e => e.LlmModel)
            .HasColumnName("llm_model")
            .IsRequired()
            .HasMaxLength(200);

        // Agent Mode
        builder.Property(e => e.AgentMode)
            .HasColumnName("agent_mode")
            .IsRequired();

        // Selected Documents (JSON array)
        builder.Property(e => e.SelectedDocumentIdsJson)
            .HasColumnName("selected_document_ids_json")
            .HasColumnType("jsonb");

        // Generation Parameters
        builder.Property(e => e.Temperature)
            .HasColumnName("temperature")
            .IsRequired()
            .HasPrecision(3, 2) // e.g., 0.75
            .HasDefaultValue(0.7m);

        builder.Property(e => e.MaxTokens)
            .HasColumnName("max_tokens")
            .IsRequired()
            .HasDefaultValue(4096);

        // Optional Overrides
        builder.Property(e => e.SystemPromptOverride)
            .HasColumnName("system_prompt_override")
            .HasMaxLength(5000);

        // Status
        builder.Property(e => e.IsCurrent)
            .HasColumnName("is_current")
            .IsRequired()
            .HasDefaultValue(false);

        // Audit
        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.CreatedBy)
            .HasColumnName("created_by")
            .IsRequired();

        // Indexes
        builder.HasIndex(e => e.AgentId)
            .HasDatabaseName("ix_agent_configurations_agent_id");

        // Only one current configuration per agent
        builder.HasIndex(e => new { e.AgentId, e.IsCurrent })
            .HasDatabaseName("ix_agent_configurations_current")
            .HasFilter("is_current = true")
            .IsUnique();

        // Relationships
        builder.HasOne(c => c.Agent)
            .WithMany()
            .HasForeignKey(c => c.AgentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

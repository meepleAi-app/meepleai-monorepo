using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
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

        // Issue #4138 — EXPAND step of the expand → migrate → contract pattern
        // (rollback-runbook §8.3). `type_value` / `type_description` were mapped to the entity
        // FIELDS `_typeValue` / `_typeDescription`, both `IsRequired()`, with an index over the
        // first. The entity no longer carries a type, but the columns MUST stay for one deploy:
        //
        //   - dropping them in the same deploy that stops reading them is NOT rollback-safe
        //     (§8.2): the previous code version mapped them as required, so rolling the code
        //     back against the new schema breaks every query on this table;
        //   - and `type_value` is NOT NULL with no default, so simply unmapping it would make
        //     every INSERT fail with 23502.
        //
        // So they are kept as NULLABLE SHADOW properties: present in the model, absent from the
        // aggregate, written as NULL, read by nobody. The index is dropped now because an index
        // is not part of any code contract. The CONTRACT deploy drops the two columns, and it is
        // the one that carries the `-- safe:` directive plus the evidence that nothing reads them.
        builder.Property<string?>("type_value")
            .HasColumnName("type_value")
            .HasMaxLength(50);
        builder.Property<string?>("type_description")
            .HasColumnName("type_description")
            .HasMaxLength(200);

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

        // Issue #1651: GIN index on kb_card_ids to support `@>` containment query
        // for the Used-by tab (agents that consume a given PDF document).
        builder.HasIndex("_kbCardIdsJson")
            .HasDatabaseName("ix_agent_definitions_kb_card_ids")
            .HasMethod("gin");

        // ChatLanguage (E5-3) - ISO 639-1 code or "auto"
        builder.Property(a => a.ChatLanguage)
            .HasColumnName("chat_language")
            .HasMaxLength(10)
            .HasDefaultValue("auto")
            .IsRequired();

        // Status lifecycle (Draft -> Testing -> Published)
        builder.Property(a => a.Status)
            .HasColumnName("status")
            .HasDefaultValue(AgentDefinitionStatus.Draft)
            .IsRequired();

        builder.HasIndex(a => a.Status).HasDatabaseName("ix_agent_definitions_status");

        builder.Property(a => a.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");

        // Indexes for search performance
        builder.HasIndex(a => a.IsActive);
        builder.HasIndex(a => a.CreatedAt);

        // New columns from agent system simplification
        builder.Property<bool>("_isSystemDefined")
            .HasColumnName("is_system_defined")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property<string?>("_typologySlug")
            .HasColumnName("typology_slug")
            .HasMaxLength(50);

        builder.Property<Guid?>("_gameId")
            .HasColumnName("game_id");

        builder.Property<int>("_invocationCount")
            .HasColumnName("invocation_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property<DateTime?>("_lastInvokedAt")
            .HasColumnName("last_invoked_at");

        builder.HasIndex("_typologySlug").HasDatabaseName("ix_agent_definitions_typology_slug");

        // Soft-delete support (Issue #904: SG3 — agent lifecycle)
        // HasQueryFilter automatically excludes soft-deleted agents from all normal queries.
        builder.Property(a => a.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(a => a.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasQueryFilter(a => !a.IsDeleted);
    }
}

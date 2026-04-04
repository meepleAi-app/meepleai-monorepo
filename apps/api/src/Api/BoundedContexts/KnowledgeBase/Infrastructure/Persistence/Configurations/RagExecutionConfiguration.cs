using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for RagExecution entity.
/// Issue #4458: RAG Execution History
/// </summary>
internal sealed class RagExecutionConfiguration : IEntityTypeConfiguration<RagExecution>
{
    public void Configure(EntityTypeBuilder<RagExecution> builder)
    {
        builder.ToTable("rag_executions", "knowledge_base");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        // Core fields - EF Core auto-discovers backing fields by convention
        builder.Property(e => e.Query)
            .HasField("_query")
            .HasColumnName("query")
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(e => e.AgentDefinitionId)
            .HasColumnName("agent_definition_id");

        builder.Property(e => e.AgentName)
            .HasColumnName("agent_name")
            .HasMaxLength(100);

        builder.Property(e => e.Strategy)
            .HasField("_strategy")
            .HasColumnName("strategy")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Model)
            .HasColumnName("model")
            .HasMaxLength(100);

        builder.Property(e => e.Provider)
            .HasColumnName("provider")
            .HasMaxLength(50);

        builder.Property(e => e.GameId)
            .HasColumnName("game_id");

        builder.Property(e => e.IsPlayground)
            .HasColumnName("is_playground")
            .HasDefaultValue(false);

        // Metrics
        builder.Property(e => e.TotalLatencyMs)
            .HasColumnName("total_latency_ms");

        builder.Property(e => e.PromptTokens)
            .HasColumnName("prompt_tokens");

        builder.Property(e => e.CompletionTokens)
            .HasColumnName("completion_tokens");

        builder.Property(e => e.TotalTokens)
            .HasColumnName("total_tokens");

        builder.Property(e => e.TotalCost)
            .HasColumnName("total_cost")
            .HasPrecision(18, 8);

        builder.Property(e => e.Confidence)
            .HasColumnName("confidence");

        builder.Property(e => e.CacheHit)
            .HasColumnName("cache_hit")
            .HasDefaultValue(false);

        builder.Property(e => e.CragVerdict)
            .HasColumnName("crag_verdict")
            .HasMaxLength(20)
            .IsRequired(false);

        // Status
        builder.Property(e => e.Status)
            .HasField("_status")
            .HasColumnName("status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(2000);

        // JSONB trace
        builder.Property(e => e.ExecutionTrace)
            .HasField("_executionTrace")
            .HasColumnName("execution_trace")
            .HasColumnType("jsonb");

        // Timestamps
        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Indexes for query performance
        builder.HasIndex(e => e.CreatedAt).HasDatabaseName("IX_rag_executions_created_at");
        builder.HasIndex(e => e.Strategy).HasDatabaseName("IX_rag_executions_strategy");
        builder.HasIndex(e => e.Status).HasDatabaseName("IX_rag_executions_status");
        builder.HasIndex(e => e.AgentDefinitionId).HasDatabaseName("IX_rag_executions_agent_definition_id");

        builder.HasIndex(e => new { e.Strategy, e.CreatedAt })
            .HasDatabaseName("IX_rag_executions_strategy_created_at")
            .IsDescending(false, true);

        // Ignore DomainEvents from base class
        builder.Ignore(e => e.DomainEvents);
    }
}

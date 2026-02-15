using Api.BoundedContexts.Administration.Domain.Aggregates.RagExecution;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.Administration;

/// <summary>
/// EF Core configuration for RagExecution entity.
/// Issue #4459: RAG Query Replay.
/// </summary>
internal sealed class RagExecutionEntityConfiguration : IEntityTypeConfiguration<RagExecution>
{
    public void Configure(EntityTypeBuilder<RagExecution> builder)
    {
        builder.ToTable("rag_executions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.StrategyId)
            .HasColumnName("strategy_id")
            .IsRequired();

        builder.Property(e => e.PipelineDefinitionJson)
            .HasColumnName("pipeline_definition_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.TestQuery)
            .HasColumnName("test_query")
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(e => e.ExecutedByUserId)
            .HasColumnName("executed_by_user_id")
            .IsRequired();

        builder.Property(e => e.Success)
            .HasColumnName("success")
            .IsRequired();

        builder.Property(e => e.TotalDurationMs)
            .HasColumnName("total_duration_ms")
            .IsRequired();

        builder.Property(e => e.TotalTokensUsed)
            .HasColumnName("total_tokens_used")
            .IsRequired();

        builder.Property(e => e.TotalCost)
            .HasColumnName("total_cost")
            .HasColumnType("numeric(18,8)")
            .IsRequired();

        builder.Property(e => e.BlocksExecuted)
            .HasColumnName("blocks_executed")
            .IsRequired();

        builder.Property(e => e.BlocksFailed)
            .HasColumnName("blocks_failed")
            .IsRequired();

        builder.Property(e => e.FinalResponse)
            .HasColumnName("final_response");

        builder.Property(e => e.ExecutionError)
            .HasColumnName("execution_error")
            .HasMaxLength(4000);

        builder.Property(e => e.EventsJson)
            .HasColumnName("events_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.ConfigOverridesJson)
            .HasColumnName("config_overrides_json")
            .HasColumnType("jsonb");

        builder.Property(e => e.ParentExecutionId)
            .HasColumnName("parent_execution_id");

        builder.Property(e => e.ExecutedAt)
            .HasColumnName("executed_at")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(e => e.DeletedAt)
            .HasColumnName("deleted_at");

        // Indexes
        builder.HasIndex(e => e.StrategyId)
            .HasDatabaseName("ix_rag_executions_strategy_id");

        builder.HasIndex(e => e.ExecutedByUserId)
            .HasDatabaseName("ix_rag_executions_executed_by_user_id");

        builder.HasIndex(e => new { e.StrategyId, e.ExecutedAt })
            .HasDatabaseName("ix_rag_executions_strategy_executed_at")
            .IsDescending(false, true);

        builder.HasIndex(e => e.ParentExecutionId)
            .HasDatabaseName("ix_rag_executions_parent_execution_id")
            .HasFilter("parent_execution_id IS NOT NULL");

        // Self-referencing FK for replay chain
        builder.HasOne<RagExecution>()
            .WithMany()
            .HasForeignKey(e => e.ParentExecutionId)
            .OnDelete(DeleteBehavior.SetNull);

        // Global query filter for soft delete
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

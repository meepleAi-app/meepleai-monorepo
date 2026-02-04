using Api.Infrastructure.Entities.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.KnowledgeBase;

/// <summary>
/// Entity Framework configuration for AgentTestResult entity.
/// Issue #3379: Agent Test Results History &amp; Persistence.
/// </summary>
internal sealed class AgentTestResultEntityConfiguration : IEntityTypeConfiguration<AgentTestResultEntity>
{
    public void Configure(EntityTypeBuilder<AgentTestResultEntity> builder)
    {
        builder.ToTable("agent_test_results");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(r => r.TypologyId)
            .HasColumnName("typology_id")
            .IsRequired();

        builder.Property(r => r.StrategyOverride)
            .HasColumnName("strategy_override")
            .HasMaxLength(50);

        builder.Property(r => r.ModelUsed)
            .HasColumnName("model_used")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.Query)
            .HasColumnName("query")
            .IsRequired();

        builder.Property(r => r.Response)
            .HasColumnName("response")
            .IsRequired();

        builder.Property(r => r.ConfidenceScore)
            .HasColumnName("confidence_score")
            .IsRequired();

        builder.Property(r => r.TokensUsed)
            .HasColumnName("tokens_used")
            .IsRequired();

        builder.Property(r => r.CostEstimate)
            .HasColumnName("cost_estimate")
            .HasPrecision(18, 8)
            .IsRequired();

        builder.Property(r => r.LatencyMs)
            .HasColumnName("latency_ms")
            .IsRequired();

        builder.Property(r => r.CitationsJson)
            .HasColumnName("citations_json")
            .HasColumnType("jsonb");

        builder.Property(r => r.ExecutedAt)
            .HasColumnName("executed_at")
            .IsRequired();

        builder.Property(r => r.ExecutedBy)
            .HasColumnName("executed_by")
            .IsRequired();

        builder.Property(r => r.Notes)
            .HasColumnName("notes")
            .HasMaxLength(2000);

        builder.Property(r => r.IsSaved)
            .HasColumnName("is_saved")
            .HasDefaultValue(false)
            .IsRequired();

        // Indexes for query performance
        builder.HasIndex(r => r.TypologyId)
            .HasDatabaseName("ix_agent_test_results_typology_id");

        builder.HasIndex(r => r.ExecutedBy)
            .HasDatabaseName("ix_agent_test_results_executed_by");

        builder.HasIndex(r => r.ExecutedAt)
            .HasDatabaseName("ix_agent_test_results_executed_at");

        builder.HasIndex(r => r.IsSaved)
            .HasDatabaseName("ix_agent_test_results_is_saved");

        builder.HasIndex(r => new { r.TypologyId, r.ExecutedAt })
            .HasDatabaseName("ix_agent_test_results_typology_executed_at");

        // Foreign key to Typology
        builder.HasOne(r => r.Typology)
            .WithMany()
            .HasForeignKey(r => r.TypologyId)
            .OnDelete(DeleteBehavior.Cascade);

        // Foreign key to User
        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.ExecutedBy)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

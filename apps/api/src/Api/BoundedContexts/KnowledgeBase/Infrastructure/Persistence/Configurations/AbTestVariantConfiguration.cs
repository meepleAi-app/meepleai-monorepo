using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for AbTestVariant entity.
/// Issue #5491: AbTestSession domain entity.
/// </summary>
public sealed class AbTestVariantConfiguration : IEntityTypeConfiguration<AbTestVariant>
{
    public void Configure(EntityTypeBuilder<AbTestVariant> builder)
    {
        builder.ToTable("ab_test_variants", "knowledge_base");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(v => v.AbTestSessionId)
            .HasColumnName("ab_test_session_id")
            .IsRequired();

        builder.Property(v => v.Label)
            .HasColumnName("label")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(v => v.Provider)
            .HasColumnName("provider")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(v => v.ModelId)
            .HasColumnName("model_id")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(v => v.Response)
            .HasColumnName("response");

        builder.Property(v => v.TokensUsed)
            .HasColumnName("tokens_used");

        builder.Property(v => v.LatencyMs)
            .HasColumnName("latency_ms");

        builder.Property(v => v.CostUsd)
            .HasColumnName("cost_usd")
            .HasPrecision(18, 8);

        builder.Property(v => v.Failed)
            .HasColumnName("failed");

        builder.Property(v => v.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(1000);

        builder.Property(v => v.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Evaluation as owned entity (columns in same table)
        builder.OwnsOne(v => v.Evaluation, eval =>
        {
            eval.Property(e => e.EvaluatorId).HasColumnName("evaluator_id");
            eval.Property(e => e.Accuracy).HasColumnName("eval_accuracy");
            eval.Property(e => e.Completeness).HasColumnName("eval_completeness");
            eval.Property(e => e.Clarity).HasColumnName("eval_clarity");
            eval.Property(e => e.Tone).HasColumnName("eval_tone");
            eval.Property(e => e.Notes).HasColumnName("eval_notes").HasMaxLength(2000);
            eval.Property(e => e.EvaluatedAt).HasColumnName("eval_evaluated_at");
        });

        // Indexes
        builder.HasIndex(v => v.AbTestSessionId);
        builder.HasIndex(v => v.ModelId);
    }
}

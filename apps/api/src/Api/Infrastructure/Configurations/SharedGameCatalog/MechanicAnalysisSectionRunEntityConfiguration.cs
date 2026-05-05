using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.SharedGameCatalog;

/// <summary>
/// EF configuration for <see cref="MechanicAnalysisSectionRunEntity"/> (ADR-051 / M1.2, B6=C).
/// Per-section provider/token tracking rows. Write-once: no updates after completion.
/// </summary>
internal sealed class MechanicAnalysisSectionRunEntityConfiguration : IEntityTypeConfiguration<MechanicAnalysisSectionRunEntity>
{
    public void Configure(EntityTypeBuilder<MechanicAnalysisSectionRunEntity> builder)
    {
        builder.ToTable("mechanic_analysis_section_runs", t =>
        {
            t.HasCheckConstraint(
                "ck_mechanic_section_runs_section_range",
                "section BETWEEN 0 AND 5");

            t.HasCheckConstraint(
                "ck_mechanic_section_runs_status_range",
                "status BETWEEN 0 AND 2");

            t.HasCheckConstraint(
                "ck_mechanic_section_runs_tokens_non_negative",
                "prompt_tokens >= 0 AND completion_tokens >= 0 AND total_tokens >= 0");

            t.HasCheckConstraint(
                "ck_mechanic_section_runs_cost_non_negative",
                "estimated_cost_usd >= 0");

            t.HasCheckConstraint(
                "ck_mechanic_section_runs_latency_non_negative",
                "latency_ms >= 0");

            t.HasCheckConstraint(
                "ck_mechanic_section_runs_error_when_failed",
                "status <> 1 OR error_message IS NOT NULL");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id").IsRequired();
        builder.Property(r => r.AnalysisId).HasColumnName("analysis_id").IsRequired();
        builder.Property(r => r.Section).HasColumnName("section").IsRequired();
        builder.Property(r => r.RunOrder).HasColumnName("run_order").IsRequired();

        builder.Property(r => r.Provider)
            .HasColumnName("provider")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(r => r.ModelUsed)
            .HasColumnName("model_used")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(r => r.PromptTokens).HasColumnName("prompt_tokens").HasDefaultValue(0).IsRequired();
        builder.Property(r => r.CompletionTokens).HasColumnName("completion_tokens").HasDefaultValue(0).IsRequired();
        builder.Property(r => r.TotalTokens).HasColumnName("total_tokens").HasDefaultValue(0).IsRequired();

        builder.Property(r => r.EstimatedCostUsd)
            .HasColumnName("estimated_cost_usd")
            .HasColumnType("numeric(12,6)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(r => r.LatencyMs).HasColumnName("latency_ms").IsRequired();
        builder.Property(r => r.Status).HasColumnName("status").IsRequired();

        builder.Property(r => r.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(4000);

        builder.Property(r => r.StartedAt).HasColumnName("started_at").IsRequired();
        builder.Property(r => r.CompletedAt).HasColumnName("completed_at").IsRequired();

        builder.HasIndex(r => r.AnalysisId).HasDatabaseName("ix_mechanic_section_runs_analysis_id");
        builder.HasIndex(r => new { r.AnalysisId, r.RunOrder })
            .HasDatabaseName("ux_mechanic_section_runs_analysis_run_order")
            .IsUnique();
        builder.HasIndex(r => r.Provider).HasDatabaseName("ix_mechanic_section_runs_provider");

        builder.HasOne(r => r.Analysis)
            .WithMany(a => a.SectionRuns)
            .HasForeignKey(r => r.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

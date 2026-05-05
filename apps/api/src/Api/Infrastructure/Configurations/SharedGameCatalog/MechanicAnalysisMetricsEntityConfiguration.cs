using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.SharedGameCatalog;

/// <summary>
/// EF configuration for <see cref="MechanicAnalysisMetricsEntity"/> (ADR-051 / M2 — per-run scoring snapshot).
/// </summary>
/// <remarks>
/// Insert-only rows (no <c>xmin</c> concurrency token, no soft-delete). Thresholds snapshot and
/// per-claim match details persist as <c>jsonb</c>. Descending <c>computed_at</c> secondary index
/// supports latest-per-game dashboard queries (Task 15).
/// </remarks>
internal sealed class MechanicAnalysisMetricsEntityConfiguration : IEntityTypeConfiguration<MechanicAnalysisMetricsEntity>
{
    public void Configure(EntityTypeBuilder<MechanicAnalysisMetricsEntity> builder)
    {
        builder.ToTable("mechanic_analysis_metrics", t =>
        {
            t.HasCheckConstraint(
                "ck_mechanic_analysis_metrics_certification_status_range",
                "certification_status BETWEEN 0 AND 2");

            t.HasCheckConstraint(
                "ck_mechanic_analysis_metrics_pct_ranges",
                "coverage_pct BETWEEN 0 AND 100 "
                + "AND page_accuracy_pct BETWEEN 0 AND 100 "
                + "AND bgg_match_pct BETWEEN 0 AND 100 "
                + "AND overall_score BETWEEN 0 AND 100");
        });

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("id").IsRequired();
        builder.Property(m => m.MechanicAnalysisId).HasColumnName("mechanic_analysis_id").IsRequired();
        builder.Property(m => m.SharedGameId).HasColumnName("shared_game_id").IsRequired();

        builder.Property(m => m.CoveragePct)
            .HasColumnName("coverage_pct")
            .HasColumnType("numeric(5,2)")
            .IsRequired();

        builder.Property(m => m.PageAccuracyPct)
            .HasColumnName("page_accuracy_pct")
            .HasColumnType("numeric(5,2)")
            .IsRequired();

        builder.Property(m => m.BggMatchPct)
            .HasColumnName("bgg_match_pct")
            .HasColumnType("numeric(5,2)")
            .IsRequired();

        builder.Property(m => m.OverallScore)
            .HasColumnName("overall_score")
            .HasColumnType("numeric(5,2)")
            .IsRequired();

        builder.Property(m => m.CertificationStatus)
            .HasColumnName("certification_status")
            .HasColumnType("int")
            .IsRequired();

        builder.Property(m => m.GoldenVersionHash)
            .HasColumnName("golden_version_hash")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(m => m.ThresholdsSnapshotJson)
            .HasColumnName("thresholds_snapshot_json")
            .HasColumnType("jsonb")
            .HasDefaultValue("{}")
            .IsRequired();

        builder.Property(m => m.MatchDetailsJson)
            .HasColumnName("match_details_json")
            .HasColumnType("jsonb")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(m => m.ComputedAt).HasColumnName("computed_at").IsRequired();

        // === Indexes ===
        builder.HasIndex(m => m.MechanicAnalysisId)
            .HasDatabaseName("ix_mechanic_analysis_metrics_analysis_id");

        // Dashboard "latest metrics per game" — Task 15.
        builder.HasIndex(m => new { m.SharedGameId, m.ComputedAt })
            .HasDatabaseName("ix_mechanic_analysis_metrics_shared_game_computed_at_desc")
            .IsDescending(false, true);

        builder.HasIndex(m => m.CertificationStatus)
            .HasDatabaseName("ix_mechanic_analysis_metrics_certification_status");

        // === FKs ===
        builder.HasOne(m => m.MechanicAnalysis)
            .WithMany()
            .HasForeignKey(m => m.MechanicAnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.SharedGame)
            .WithMany()
            .HasForeignKey(m => m.SharedGameId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

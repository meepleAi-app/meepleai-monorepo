using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.SharedGameCatalog;

/// <summary>
/// EF configuration for <see cref="CertificationThresholdsConfigEntity"/> (ADR-051 / M2 — singleton thresholds config).
/// </summary>
/// <remarks>
/// Singleton row with fixed <c>Id = 1</c> (no auto-generation); seed data is added separately.
/// Optimistic concurrency via PostgreSQL's system <c>xmin</c>. No FKs — this is a global
/// configuration row.
/// </remarks>
internal sealed class CertificationThresholdsConfigEntityConfiguration : IEntityTypeConfiguration<CertificationThresholdsConfigEntity>
{
    public void Configure(EntityTypeBuilder<CertificationThresholdsConfigEntity> builder)
    {
        builder.ToTable("certification_thresholds_config", t =>
        {
            // Singleton: exactly one row with Id = 1.
            t.HasCheckConstraint(
                "ck_certification_thresholds_config_singleton",
                "id = 1");

            t.HasCheckConstraint(
                "ck_certification_thresholds_config_pct_ranges",
                "min_coverage_pct BETWEEN 0 AND 100 "
                + "AND min_bgg_match_pct BETWEEN 0 AND 100 "
                + "AND min_overall_score BETWEEN 0 AND 100");

            t.HasCheckConstraint(
                "ck_certification_thresholds_config_max_page_tolerance_non_negative",
                "max_page_tolerance >= 0");
        });

        builder.HasKey(c => c.Id);

        // Explicit Id (singleton); do not auto-generate.
        builder.Property(c => c.Id)
            .HasColumnName("id")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(c => c.MinCoveragePct)
            .HasColumnName("min_coverage_pct")
            .HasColumnType("numeric(5,2)")
            .IsRequired();

        builder.Property(c => c.MaxPageTolerance)
            .HasColumnName("max_page_tolerance")
            .IsRequired();

        builder.Property(c => c.MinBggMatchPct)
            .HasColumnName("min_bgg_match_pct")
            .HasColumnType("numeric(5,2)")
            .IsRequired();

        builder.Property(c => c.MinOverallScore)
            .HasColumnName("min_overall_score")
            .HasColumnType("numeric(5,2)")
            .IsRequired();

        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(c => c.UpdatedByUserId).HasColumnName("updated_by_user_id");

        // Optimistic concurrency via PostgreSQL's system `xmin`.
        builder.Property(c => c.Xmin)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}

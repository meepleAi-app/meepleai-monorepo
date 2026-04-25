using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.SharedGameCatalog;

/// <summary>
/// EF configuration for <see cref="MechanicAnalysisEntity"/> (ADR-051 / M1.1).
/// </summary>
/// <remarks>
/// Enforces at DB level:
/// - Partial unique index: at most one <c>Status=Published AND IsSuppressed=false</c> per SharedGame.
/// - CHECK (cost_cap_override all-or-none).
/// - CHECK (suppression completeness — unsuppressed rows must have all suppression fields NULL;
///   suppressed rows must have actor + reason + suppressed_at populated).
/// - Global query filter hides suppressed rows from player-facing queries.
/// </remarks>
internal sealed class MechanicAnalysisEntityConfiguration : IEntityTypeConfiguration<MechanicAnalysisEntity>
{
    public void Configure(EntityTypeBuilder<MechanicAnalysisEntity> builder)
    {
        builder.ToTable("mechanic_analyses", t =>
        {
            t.HasCheckConstraint(
                "ck_mechanic_analyses_cost_cap_override_all_or_none",
                "(cost_cap_override_at IS NULL AND cost_cap_override_by IS NULL AND cost_cap_override_reason IS NULL) "
                + "OR (cost_cap_override_at IS NOT NULL AND cost_cap_override_by IS NOT NULL AND cost_cap_override_reason IS NOT NULL)");

            t.HasCheckConstraint(
                "ck_mechanic_analyses_suppression_completeness",
                "(is_suppressed = false AND suppressed_at IS NULL AND suppressed_by IS NULL "
                + "AND suppression_reason IS NULL AND suppression_request_source IS NULL "
                + "AND suppression_requested_at IS NULL) "
                + "OR (is_suppressed = true AND suppressed_at IS NOT NULL AND suppressed_by IS NOT NULL "
                + "AND suppression_reason IS NOT NULL)");

            t.HasCheckConstraint(
                "ck_mechanic_analyses_status_range",
                "status BETWEEN 0 AND 3");

            t.HasCheckConstraint(
                "ck_mechanic_analyses_cost_cap_positive",
                "cost_cap_usd > 0");
        });

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id").IsRequired();
        builder.Property(a => a.SharedGameId).HasColumnName("shared_game_id").IsRequired();
        builder.Property(a => a.PdfDocumentId).HasColumnName("pdf_document_id").IsRequired();

        builder.Property(a => a.PromptVersion)
            .HasColumnName("prompt_version")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(a => a.Status).HasColumnName("status").IsRequired();
        builder.Property(a => a.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.ReviewedBy).HasColumnName("reviewed_by");
        builder.Property(a => a.ReviewedAt).HasColumnName("reviewed_at");

        builder.Property(a => a.RejectionReason)
            .HasColumnName("rejection_reason")
            .HasMaxLength(2000);

        builder.Property(a => a.TotalTokensUsed).HasColumnName("total_tokens_used").HasDefaultValue(0).IsRequired();

        builder.Property(a => a.EstimatedCostUsd)
            .HasColumnName("estimated_cost_usd")
            .HasColumnType("numeric(12,6)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(a => a.ModelUsed).HasColumnName("model_used").HasMaxLength(128).IsRequired();
        builder.Property(a => a.Provider).HasColumnName("provider").HasMaxLength(64).IsRequired();

        builder.Property(a => a.CostCapUsd)
            .HasColumnName("cost_cap_usd")
            .HasColumnType("numeric(12,6)")
            .IsRequired();

        builder.Property(a => a.CostCapOverrideAt).HasColumnName("cost_cap_override_at");
        builder.Property(a => a.CostCapOverrideBy).HasColumnName("cost_cap_override_by");
        builder.Property(a => a.CostCapOverrideReason).HasColumnName("cost_cap_override_reason").HasMaxLength(2000);

        builder.Property(a => a.IsSuppressed).HasColumnName("is_suppressed").HasDefaultValue(false).IsRequired();
        builder.Property(a => a.SuppressedAt).HasColumnName("suppressed_at");
        builder.Property(a => a.SuppressedBy).HasColumnName("suppressed_by");
        builder.Property(a => a.SuppressionReason).HasColumnName("suppression_reason").HasMaxLength(2000);
        builder.Property(a => a.SuppressionRequestedAt).HasColumnName("suppression_requested_at");
        builder.Property(a => a.SuppressionRequestSource).HasColumnName("suppression_request_source");

        // === AI comprehension certification (ADR-051 M2) ===
        builder.Property(a => a.CertificationStatus)
            .HasColumnName("certification_status")
            .HasDefaultValue(0)
            .IsRequired();
        builder.Property(a => a.CertifiedAt).HasColumnName("certified_at");
        builder.Property(a => a.CertifiedByUserId).HasColumnName("certified_by_user_id");
        builder.Property(a => a.CertificationOverrideReason)
            .HasColumnName("certification_override_reason")
            .HasMaxLength(500);
        builder.Property(a => a.LastMetricsId).HasColumnName("last_metrics_id");

        // Optimistic concurrency via PostgreSQL's system `xmin` column, mapped to an explicit
        // property on the entity so the repository can round-trip its value through the aggregate
        // (preserving the original token on detached Update).
        builder.Property(a => a.Xmin)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // === Indexes ===
        builder.HasIndex(a => a.SharedGameId).HasDatabaseName("ix_mechanic_analyses_shared_game_id");
        builder.HasIndex(a => a.PdfDocumentId).HasDatabaseName("ix_mechanic_analyses_pdf_document_id");
        builder.HasIndex(a => a.Status).HasDatabaseName("ix_mechanic_analyses_status");
        builder.HasIndex(a => a.CreatedBy).HasDatabaseName("ix_mechanic_analyses_created_by");
        builder.HasIndex(a => a.IsSuppressed)
            .HasDatabaseName("ix_mechanic_analyses_is_suppressed")
            .HasFilter("is_suppressed = true");

        builder.HasIndex(a => a.CertificationStatus)
            .HasDatabaseName("ix_mechanic_analyses_certification_status");

        // One Published, non-suppressed analysis per shared game (plan §2.2.1).
        builder.HasIndex(a => a.SharedGameId)
            .HasDatabaseName("ux_mechanic_analyses_published_per_game")
            .IsUnique()
            .HasFilter("status = 2 AND is_suppressed = false");

        // T7 reproducibility (plan §2.2 + §3.5): one non-rejected analysis per
        // (SharedGameId, PdfDocumentId, PromptVersion) tuple. Excluding Rejected (status=3)
        // lets users re-run the same prompt after a rejection without collision,
        // while a new PromptVersion bump always produces a fresh row.
        builder.HasIndex(a => new { a.SharedGameId, a.PdfDocumentId, a.PromptVersion })
            .HasDatabaseName("ux_mechanic_analyses_shared_game_pdf_prompt")
            .IsUnique()
            .HasFilter("status <> 3");

        // === FK to SharedGame ===
        builder.HasOne(a => a.SharedGame)
            .WithMany()
            .HasForeignKey(a => a.SharedGameId)
            .OnDelete(DeleteBehavior.Cascade);

        // === Children ===
        builder.HasMany(a => a.Claims)
            .WithOne(c => c.Analysis)
            .HasForeignKey(c => c.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        // === Suppression global query filter (T5) ===
        builder.HasQueryFilter(a => !a.IsSuppressed);
    }
}

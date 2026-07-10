using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.SharedGameCatalog;

/// <summary>
/// EF configuration for <see cref="MechanicCardEntity"/> (ADR-051 / #527).
/// </summary>
/// <remarks>
/// Enforces at DB level:
/// - Partial unique index: at most one non-suppressed card per SharedGame (T5 / AC-1).
/// - CHECK origin IN ('ai_reviewed','manual','imported_external'), version >= 1.
/// - CHECK suppression completeness (suppressed rows carry actor + reason + timestamp).
/// - Global query filter hides suppressed cards from player-facing queries.
/// </remarks>
internal sealed class MechanicCardEntityConfiguration : IEntityTypeConfiguration<MechanicCardEntity>
{
    public void Configure(EntityTypeBuilder<MechanicCardEntity> builder)
    {
        builder.ToTable("mechanic_cards", t =>
        {
            t.HasCheckConstraint(
                "ck_mechanic_cards_origin",
                "origin IN ('ai_reviewed', 'manual', 'imported_external')");

            t.HasCheckConstraint(
                "ck_mechanic_cards_version_positive",
                "version >= 1");

            t.HasCheckConstraint(
                "ck_mechanic_cards_suppression_completeness",
                "(is_suppressed = false AND suppressed_at IS NULL AND suppressed_by IS NULL AND suppressed_reason IS NULL) "
                + "OR (is_suppressed = true AND suppressed_at IS NOT NULL AND suppressed_by IS NOT NULL AND suppressed_reason IS NOT NULL)");

            t.HasCheckConstraint(
                "ck_mechanic_cards_error_reports_non_negative",
                "error_reports_count >= 0");
        });

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id").IsRequired();
        builder.Property(c => c.SharedGameId).HasColumnName("shared_game_id").IsRequired();
        builder.Property(c => c.OriginAnalysisId).HasColumnName("origin_analysis_id").IsRequired();
        builder.Property(c => c.Origin).HasColumnName("origin").HasMaxLength(32).IsRequired();
        builder.Property(c => c.Title).HasColumnName("title").HasMaxLength(200).IsRequired();

        builder.Property(c => c.Content)
            .HasColumnName("content")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(c => c.Version).HasColumnName("version").HasDefaultValue(1).IsRequired();

        builder.Property(c => c.IsSuppressed).HasColumnName("is_suppressed").HasDefaultValue(false).IsRequired();
        builder.Property(c => c.SuppressedReason).HasColumnName("suppressed_reason").HasMaxLength(500);
        builder.Property(c => c.SuppressedAt).HasColumnName("suppressed_at");
        builder.Property(c => c.SuppressedBy).HasColumnName("suppressed_by");

        builder.Property(c => c.ErrorReportsCount).HasColumnName("error_reports_count").HasDefaultValue(0).IsRequired();
        builder.Property(c => c.FeedbackScore).HasColumnName("feedback_score").HasColumnType("numeric(4,2)");

        builder.Property(c => c.PublishedAt).HasColumnName("published_at").IsRequired();
        builder.Property(c => c.PublishedBy).HasColumnName("published_by").IsRequired();
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.Property(c => c.Xmin)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // === Indexes ===
        // At most one active (non-suppressed) card per shared game (AC-1 / T5).
        builder.HasIndex(c => c.SharedGameId)
            .HasDatabaseName("ux_mechanic_cards_active_per_game")
            .IsUnique()
            .HasFilter("is_suppressed = false");

        builder.HasIndex(c => c.OriginAnalysisId)
            .HasDatabaseName("ix_mechanic_cards_origin_analysis_id");

        // === FKs ===
        builder.HasOne(c => c.SharedGame)
            .WithMany()
            .HasForeignKey(c => c.SharedGameId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.OriginAnalysis)
            .WithMany()
            .HasForeignKey(c => c.OriginAnalysisId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.AuditLog)
            .WithOne(a => a.Card)
            .HasForeignKey(a => a.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        // Player-facing default hides suppressed cards (admin ops use IgnoreQueryFilters).
        builder.HasQueryFilter(c => !c.IsSuppressed);
    }
}

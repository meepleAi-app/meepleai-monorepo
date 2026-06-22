using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.SharedGameCatalog;

/// <summary>
/// EF Core fluent configuration for <see cref="WikidataCoverEnrichmentAttemptEntity"/>.
/// Issue #1823 Wave 3 M9.
/// </summary>
internal sealed class WikidataCoverEnrichmentAttemptEntityConfiguration
    : IEntityTypeConfiguration<WikidataCoverEnrichmentAttemptEntity>
{
    public void Configure(EntityTypeBuilder<WikidataCoverEnrichmentAttemptEntity> builder)
    {
        builder.ToTable("wikidata_cover_enrichment_attempts");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.SharedGameId)
            .HasColumnName("shared_game_id")
            .IsRequired();

        builder.Property(e => e.AttemptedAt)
            .HasColumnName("attempted_at")
            .IsRequired();

        builder.Property(e => e.Outcome)
            .HasColumnName("outcome")
            .IsRequired();

        builder.Property(e => e.Reason)
            .HasColumnName("reason")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.Details)
            .HasColumnName("details")
            .HasMaxLength(1024)
            .IsRequired(false);

        builder.Property(e => e.RetryCount)
            .HasColumnName("retry_count")
            .IsRequired();

        builder.Property(e => e.NextRetryAt)
            .HasColumnName("next_retry_at")
            .IsRequired(false);

        builder.Property(e => e.DeadLetteredAt)
            .HasColumnName("dead_lettered_at")
            .IsRequired(false);

        // Foreign key to shared_games (cascade delete: if the catalog game is hard-
        // deleted, drop the attempt history too — soft-deletes are filtered out by
        // the scheduler query directly).
        builder.HasOne<SharedGameEntity>()
            .WithMany()
            .HasForeignKey(e => e.SharedGameId)
            .OnDelete(DeleteBehavior.Cascade);

        // Query optimization: "latest attempt per game" + "due retries" cover the
        // two M9 scheduler hot paths.
        builder.HasIndex(e => new { e.SharedGameId, e.AttemptedAt })
            .HasDatabaseName("ix_wikidata_cover_attempts_game_latest")
            .IsDescending(false, true);

        builder.HasIndex(e => e.NextRetryAt)
            .HasDatabaseName("ix_wikidata_cover_attempts_next_retry")
            .HasFilter("next_retry_at IS NOT NULL");

        builder.HasIndex(e => e.DeadLetteredAt)
            .HasDatabaseName("ix_wikidata_cover_attempts_dead_letter")
            .HasFilter("dead_lettered_at IS NOT NULL");

        // Issue #2254 (F5) — bulk acknowledge metadata.
        builder.Property(e => e.AcknowledgedAt)
            .HasColumnName("acknowledged_at")
            .IsRequired(false);

        builder.Property(e => e.AcknowledgedBy)
            .HasColumnName("acknowledged_by")
            .IsRequired(false);

        // Issue #2255 (F6) — admin-triggered attribution (null for M9 scheduler).
        builder.Property(e => e.TriggeredByAdminUserId)
            .HasColumnName("triggered_by_admin_user_id")
            .IsRequired(false);

        // F5 partial index: speeds up default list view (exclude acked = WHERE acknowledged_at IS NULL).
        builder.HasIndex(e => e.AcknowledgedAt)
            .HasDatabaseName("ix_wikidata_cover_attempts_acknowledged_at")
            .HasFilter("acknowledged_at IS NOT NULL");
    }
}

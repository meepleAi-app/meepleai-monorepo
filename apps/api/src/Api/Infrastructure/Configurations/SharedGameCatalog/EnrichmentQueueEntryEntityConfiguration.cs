using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.SharedGameCatalog;

/// <summary>
/// EF configuration for <see cref="EnrichmentQueueEntryEntity"/> (#1874).
/// Table <c>enrichment_queue_entries</c>: queued BGG enrichment requests.
/// </summary>
internal sealed class EnrichmentQueueEntryEntityConfiguration : IEntityTypeConfiguration<EnrichmentQueueEntryEntity>
{
    public void Configure(EntityTypeBuilder<EnrichmentQueueEntryEntity> builder)
    {
        builder.ToTable("enrichment_queue_entries", t =>
        {
            t.HasCheckConstraint(
                "ck_enrichment_queue_entries_priority_range",
                "priority BETWEEN 0 AND 2");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(e => e.SharedGameId)
            .HasColumnName("shared_game_id")
            .IsRequired();

        // Sentinel = -1 (not a valid EnrichmentPriority) so EF does NOT treat 0 (Stale)
        // as the "unset" default and therefore always emits Priority in INSERT statements.
        // Without this, Stale entries would be silently mapped to whatever the column
        // default is. No HasDefaultValue on the column itself either.
        builder.Property(e => e.Priority)
            .HasColumnName("priority")
            .HasSentinel((EnrichmentPriority)(-1))
            .IsRequired();

        builder.Property(e => e.QueuedAt)
            .HasColumnName("queued_at")
            .IsRequired();

        builder.Property(e => e.Reason)
            .HasColumnName("reason")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.QueuedByUserId)
            .HasColumnName("queued_by_user_id");

        builder.Property(e => e.IsProcessed)
            .HasColumnName("is_processed")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(e => e.ProcessedAt)
            .HasColumnName("processed_at");

        // === Indexes ===
        // Primary access pattern: queue listing ordered by priority DESC, queued_at ASC, filtered by IsProcessed=false
        builder.HasIndex(e => new { e.IsProcessed, e.Priority, e.QueuedAt })
            .HasDatabaseName("ix_enrichment_queue_entries_pending_listing")
            .HasFilter("is_processed = false");

        builder.HasIndex(e => e.SharedGameId)
            .HasDatabaseName("ix_enrichment_queue_entries_shared_game_id");

        // === FKs ===
        builder.HasOne(e => e.SharedGame)
            .WithMany()
            .HasForeignKey(e => e.SharedGameId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.QueuedByUser)
            .WithMany()
            .HasForeignKey(e => e.QueuedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

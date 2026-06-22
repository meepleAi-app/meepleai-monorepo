using Api.BoundedContexts.DocumentProcessing.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.DocumentProcessing;

/// <summary>
/// EF Core configuration for <see cref="StorageOperationOutboxEntity"/>
/// (issue #1314 PR 2). Maps to <c>storage_operation_outbox</c> table.
/// Drainer query (<c>WHERE status='Pending' AND scheduled_at &lt;= now ORDER BY scheduled_at</c>)
/// is served by the composite index on (Status, ScheduledAt) — mirrors the
/// EmailOutbox processor index.
/// </summary>
internal class StorageOperationOutboxEntityConfiguration : IEntityTypeConfiguration<StorageOperationOutboxEntity>
{
    public void Configure(EntityTypeBuilder<StorageOperationOutboxEntity> builder)
    {
        builder.ToTable("storage_operation_outbox");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).IsRequired();
        builder.Property(e => e.MigrationId).IsRequired();
        builder.Property(e => e.LegacyKey).IsRequired().HasMaxLength(512);
        builder.Property(e => e.NewKey).IsRequired().HasMaxLength(512);
        builder.Property(e => e.Category).IsRequired().HasMaxLength(64);
        builder.Property(e => e.ResourceKey).IsRequired().HasMaxLength(128);
        builder.Property(e => e.ScheduledAt).IsRequired();
        builder.Property(e => e.SentAt);
        builder.Property(e => e.AttemptCount).IsRequired().HasDefaultValue(0);
        builder.Property(e => e.LastError).HasMaxLength(2000);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.Status).IsRequired().HasMaxLength(32).HasDefaultValue("Pending");

        // Drainer pickup pattern: WHERE status='Pending' AND scheduled_at <= now ORDER BY scheduled_at
        builder.HasIndex(e => new { e.Status, e.ScheduledAt })
            .HasDatabaseName("IX_storage_operation_outbox_status_scheduled_at");

        // Correlation lookups: per-migration-run progress queries + dashboards.
        builder.HasIndex(e => e.MigrationId)
            .HasDatabaseName("IX_storage_operation_outbox_migration_id");

        // Idempotency at the move level: re-running a migration script must not
        // produce duplicate rows for the same legacy key. Caller of EnqueueAsync
        // checks this unique constraint to skip already-enqueued moves.
        builder.HasIndex(e => e.LegacyKey)
            .HasDatabaseName("IX_storage_operation_outbox_legacy_key")
            .IsUnique();
    }
}

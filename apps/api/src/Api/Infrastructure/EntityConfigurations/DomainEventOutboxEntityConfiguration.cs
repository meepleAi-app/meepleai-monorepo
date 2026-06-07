using Api.Infrastructure.Entities.DomainEventOutbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

/// <summary>
/// EF Core configuration for <see cref="DomainEventOutboxEntity"/> (issue #1535).
///
/// <para>Two partial indexes scope storage costs while keeping hot paths fast:</para>
/// <list type="bullet">
///   <item><c>ix_domain_event_outbox_pending</c> on <c>(next_attempt_at, enqueued_at)</c>
///         filtered to <c>Status = Pending</c> — the processor's poll query path.
///         Pending rows are a small minority of the cumulative row count, so the partial
///         index stays tiny.</item>
///   <item><c>ix_domain_event_outbox_failed_recent</c> on <c>enqueued_at DESC</c>
///         filtered to <c>Status = Failed</c> — the admin dashboard's "recent failures" feed.</item>
/// </list>
///
/// <para>Sent rows are NOT indexed. They are subject to TTL cleanup (30-day retention,
/// see follow-up issue) and don't participate in the read-mostly paths.</para>
/// </summary>
internal sealed class DomainEventOutboxEntityConfiguration : IEntityTypeConfiguration<DomainEventOutboxEntity>
{
    public void Configure(EntityTypeBuilder<DomainEventOutboxEntity> builder)
    {
        builder.ToTable("domain_event_outbox");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(e => e.EventType)
            .HasColumnName("event_type")
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.PayloadJson)
            .HasColumnName("payload_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.PayloadVersion)
            .HasColumnName("payload_version")
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasConversion<byte>()
            .IsRequired();

        builder.Property(e => e.Attempts)
            .HasColumnName("attempts")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.LastError)
            .HasColumnName("last_error")
            .HasMaxLength(2048);

        builder.Property(e => e.OccurredAt)
            .HasColumnName("occurred_at")
            .IsRequired();

        builder.Property(e => e.EnqueuedAt)
            .HasColumnName("enqueued_at")
            .IsRequired();

        builder.Property(e => e.DispatchedAt)
            .HasColumnName("dispatched_at");

        builder.Property(e => e.NextAttemptAt)
            .HasColumnName("next_attempt_at");

        builder.Property(e => e.CorrelationId)
            .HasColumnName("correlation_id")
            .HasMaxLength(128);

        // F6 (Issue #1535 T6 code review): optimistic concurrency token via Postgres-native
        // `xmin` (the row's transaction id, automatically updated on every UPDATE). Mapping
        // to `xmin` avoids a real RowVersion column and any associated migration — Npgsql
        // handles the IsRowVersion/IsConcurrencyToken pair as a shadow read of the system
        // column. Every UPDATE that doesn't match the loaded xmin raises
        // DbUpdateConcurrencyException, surfacing concurrent admin/processor races as a
        // catchable signal instead of a silent lost-update.
        builder.Property(e => e.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // Hot path: processor poll. Partial index — Sent/Failed rows excluded.
        // Ordering: NextAttemptAt then EnqueuedAt. Rows with NextAttemptAt NULL
        // (first attempt) collate ahead of those with a future schedule.
        //
        // F8: filter literal MUST be smallint-typed (`0::smallint`) for the Postgres
        // planner to match the partial-index predicate against the Npgsql-parameterised
        // query (`WHERE status = @p0` with @p0 typed smallint). Without the explicit
        // cast, predicate matching can degrade to seq-scan on the hot poll path.
        builder.HasIndex(e => new { e.NextAttemptAt, e.EnqueuedAt })
            .HasDatabaseName("ix_domain_event_outbox_pending")
            .HasFilter("status = 0::smallint");

        // Ops dashboard: list recent terminal failures. Same smallint literal rationale.
        builder.HasIndex(e => e.EnqueuedAt)
            .IsDescending()
            .HasDatabaseName("ix_domain_event_outbox_failed_recent")
            .HasFilter("status = 2::smallint");
    }
}

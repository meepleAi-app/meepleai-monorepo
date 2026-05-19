using Api.Infrastructure.Entities.DomainEventLog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

/// <summary>
/// EF Core configuration for <see cref="DomainEventLogEntity"/>.
/// Issue #661: append-only durable log of domain events. Three indexes
/// per spec §3.1 hardened:
///   - <c>(UserId, LoggedAt DESC)</c>: primary query path for the activity feed.
///   - <c>(LoggedAt)</c>: enables an eventual cleanup-job range scan (P1-1).
///   - UNIQUE on <c>EventId</c>: idempotency guard (P1-3, AC-11).
/// </summary>
internal class DomainEventLogEntityConfiguration : IEntityTypeConfiguration<DomainEventLogEntity>
{
    public void Configure(EntityTypeBuilder<DomainEventLogEntity> builder)
    {
        builder.ToTable("domain_event_logs");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).IsRequired();
        builder.Property(e => e.EventId).IsRequired();
        builder.Property(e => e.EventType).IsRequired().HasMaxLength(128);
        builder.Property(e => e.UserId);
        builder.Property(e => e.AggregateId);
        builder.Property(e => e.AggregateType).HasMaxLength(64);
        // PayloadJson uses jsonb for queryability + storage compression.
        builder.Property(e => e.PayloadJson)
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(e => e.OccurredAt).IsRequired();
        builder.Property(e => e.LoggedAt).IsRequired();

        // Primary query path: activity feed reads `WHERE UserId = caller AND LoggedAt >= cutoff ORDER BY LoggedAt DESC`.
        builder.HasIndex(e => new { e.UserId, e.LoggedAt })
            .HasDatabaseName("ix_domain_event_logs_user_loggedat")
            .IsDescending(false, true);

        // Standalone LoggedAt index for a future cleanup-job scan
        // (DELETE FROM ... WHERE LoggedAt < cutoff). Tracked as a separate
        // ops ticket; index is cheap to ship now (P1-1 panel fix).
        builder.HasIndex(e => e.LoggedAt)
            .HasDatabaseName("ix_domain_event_logs_loggedat");

        // Idempotency guard: a retry that re-emits the same EventId must
        // fail rather than create duplicate log rows (P1-3, AC-11).
        builder.HasIndex(e => e.EventId)
            .IsUnique()
            .HasDatabaseName("ux_domain_event_logs_eventid");
    }
}

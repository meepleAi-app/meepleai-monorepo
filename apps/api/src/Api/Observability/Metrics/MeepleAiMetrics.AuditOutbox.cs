// SP5 Admin Security S1 T4b — audit outbox health gauges
using System.Diagnostics.Metrics;
using Api.BoundedContexts.Administration.Infrastructure.Health;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    private static bool _auditOutboxGaugesRegistered;

    /// <summary>
    /// Registers ObservableGauges that report the latest snapshot of the audit_outbox health
    /// counters from the singleton <see cref="IAuditOutboxHealthTracker"/>. Idempotent —
    /// subsequent calls are ignored.
    ///
    /// Gauges:
    ///   • <c>meepleai.audit.outbox.pending.count</c>       — Pending rows awaiting drain.
    ///   • <c>meepleai.audit.outbox.pending.oldest_age_seconds</c> — age of oldest Pending row.
    ///   • <c>meepleai.audit.outbox.failed.count</c>        — Failed rows (poison messages).
    ///
    /// Suggested alerting:
    ///   • Pending count steadily increasing → processor stalled.
    ///   • oldest_age_seconds &gt; 60 → drain falling behind ingestion.
    ///   • Failed count &gt; 0 → poison message in queue; operator must inspect via dashboard.
    /// </summary>
    public static void RegisterAuditOutboxGauges(IAuditOutboxHealthTracker tracker)
    {
        if (_auditOutboxGaugesRegistered)
        {
            return;
        }
        _auditOutboxGaugesRegistered = true;

        Meter.CreateObservableGauge(
            name: "meepleai.audit.outbox.pending.count",
            observeValue: () => tracker.GetPendingCount(),
            unit: "rows",
            description: "Number of audit_outbox rows currently in Pending status.");

        Meter.CreateObservableGauge(
            name: "meepleai.audit.outbox.pending.oldest_age_seconds",
            observeValue: () => tracker.GetOldestPendingAgeSeconds(),
            unit: "s",
            description: "Age in seconds of the oldest Pending audit_outbox row (0 when the queue is empty).");

        Meter.CreateObservableGauge(
            name: "meepleai.audit.outbox.failed.count",
            observeValue: () => tracker.GetFailedCount(),
            unit: "rows",
            description: "Number of audit_outbox rows currently in Failed status (poison messages awaiting operator intervention).");
    }

    /// <summary>
    /// Test-only reset hook so tests that exercise <see cref="RegisterAuditOutboxGauges"/>
    /// can be made idempotent across multiple instantiations. NOT for production use.
    /// </summary>
    internal static void ResetAuditOutboxGaugesForTest()
    {
        _auditOutboxGaugesRegistered = false;
    }
}

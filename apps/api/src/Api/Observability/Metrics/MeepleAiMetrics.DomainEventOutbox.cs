// Issue #1535 T6 — domain_event_outbox counters + health gauges
using System.Diagnostics.Metrics;
using Api.Infrastructure.DomainEventOutbox;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    private static bool _domainEventOutboxGaugesRegistered;

    /// <summary>
    /// Incremented once per row inserted into <c>domain_event_outbox</c> by
    /// <c>MeepleAiDbContext.SaveChangesAsync</c> (Hybrid + OutboxOnly modes).
    /// Tag: <c>event_type</c> — registry alias when available, CLR <c>FullName</c> fallback.
    ///
    /// <para>Used to compute <i>arrival rate</i>. Pair with
    /// <see cref="DomainEventOutboxDispatched"/> in dashboards to spot a widening gap
    /// (rate(enqueued) − rate(dispatched) ≈ 0 means the processor is keeping up).</para>
    /// </summary>
    public static readonly Counter<long> DomainEventOutboxEnqueued = Meter.CreateCounter<long>(
        name: "meepleai.domain_event_outbox.enqueued.total",
        unit: "events",
        description: "Total outbox rows INSERTed by the DbContext (#1535 T6).");

    /// <summary>
    /// Incremented once per successful <c>MediatR.Publish</c> from
    /// <c>DomainEventOutboxProcessor</c>. Tag: <c>event_type</c> (same labelling as
    /// <see cref="DomainEventOutboxEnqueued"/>).
    ///
    /// <para>Used to compute <i>throughput</i>. The dispatched rate must converge with the
    /// enqueued rate over a sliding window (otherwise backlog grows — see
    /// <see cref="MeepleAiMetrics"/> health gauges below).</para>
    /// </summary>
    public static readonly Counter<long> DomainEventOutboxDispatched = Meter.CreateCounter<long>(
        name: "meepleai.domain_event_outbox.dispatched.total",
        unit: "events",
        description: "Total outbox rows transitioned Pending → Sent (#1535 T6).");

    /// <summary>
    /// Incremented when the processor's catch branch takes the
    /// <c>MarkRetry</c> path (failure with budget remaining). Tag: <c>event_type</c>.
    ///
    /// <para>Spikes here signal a transient consumer outage. Distinguish from
    /// <see cref="DomainEventOutboxFailedTerminal"/> — retried rows return to Pending and
    /// will eventually dispatch; terminal rows require ops intervention.</para>
    /// </summary>
    public static readonly Counter<long> DomainEventOutboxRetried = Meter.CreateCounter<long>(
        name: "meepleai.domain_event_outbox.retried.total",
        unit: "events",
        description: "Total outbox rows scheduled for retry after a transient dispatch failure (#1535 T6).");

    /// <summary>
    /// Incremented when the processor exhausts the <c>MaxAttempts</c> budget and transitions
    /// the row to Failed (terminal). Tag: <c>event_type</c>.
    ///
    /// <para>This is the ops-paging signal: any non-zero increment over a 10-minute window
    /// indicates poison messages requiring manual triage. Alert rule in
    /// <c>prometheus-alerts.yml</c>.</para>
    /// </summary>
    public static readonly Counter<long> DomainEventOutboxFailedTerminal = Meter.CreateCounter<long>(
        name: "meepleai.domain_event_outbox.failed_terminal.total",
        unit: "events",
        description: "Total outbox rows transitioned to terminal Failed after retry budget exhaustion (#1535 T6).");

    /// <summary>
    /// Incremented once per row purged by <c>DomainEventOutboxRetentionService</c>
    /// (Issue #1966). Tag: <c>event_type</c>. Operators can verify the retention TTL
    /// is draining the Sent partition at the expected rate by comparing
    /// <c>rate(purged.total)</c> to <c>rate(dispatched.total)</c> shifted by
    /// <see cref="DomainEventOutboxOptions.SentRetentionDays"/>.
    /// </summary>
    public static readonly Counter<long> DomainEventOutboxPurged = Meter.CreateCounter<long>(
        name: "meepleai.domain_event_outbox.purged.total",
        unit: "events",
        description: "Total Sent outbox rows purged by the retention background service (#1966).");

    /// <summary>
    /// Distribution of end-to-end dispatch latency: <c>(MarkSent.now − EnqueuedAt)</c> in
    /// seconds, recorded ONCE per row that transitions Pending → Sent. Tag: <c>event_type</c>.
    /// Bucket bounds chosen for the DoD-9 SLO (p95 &lt; 10s): tight resolution under 10s,
    /// coarser past the SLO so a regression spike still lands in a meaningful bucket.
    ///
    /// <para>Issue #1535 T8 follow-up: replaces the <c>pending_oldest_age_seconds</c> proxy
    /// used by the Phase A/B runbook. The histogram is the canonical signal for the
    /// DoD-9 latency gate:</para>
    /// <code>histogram_quantile(0.95, rate(meepleai_domain_event_outbox_dispatch_latency_seconds_bucket[5m]))</code>
    /// </summary>
    public static readonly Histogram<double> DomainEventOutboxDispatchLatencySeconds = Meter.CreateHistogram<double>(
        name: "meepleai.domain_event_outbox.dispatch_latency_seconds",
        unit: "s",
        description: "End-to-end dispatch latency from EnqueuedAt to MarkSent in seconds (#1535 T8 follow-up).",
        advice: new InstrumentAdvice<double>
        {
            HistogramBucketBoundaries = new[] { 0.05, 0.1, 0.25, 0.5, 1.0, 2.5, 5.0, 7.5, 10.0, 15.0, 30.0, 60.0, 120.0, 300.0 },
        });

    /// <summary>
    /// Registers the three <c>ObservableGauges</c> that report the latest health snapshot
    /// from the singleton <see cref="IDomainEventOutboxHealthTracker"/>. Idempotent — repeat
    /// calls are a no-op. Mirrors <see cref="RegisterAuditOutboxGauges"/>.
    ///
    /// <list type="bullet">
    ///   <item><c>meepleai.domain_event_outbox.pending.count</c> — Pending rows awaiting dispatch.</item>
    ///   <item><c>meepleai.domain_event_outbox.pending.oldest_age_seconds</c> — age of the oldest Pending row.</item>
    ///   <item><c>meepleai.domain_event_outbox.failed.count</c> — Failed rows (terminal, ops-visible).</item>
    /// </list>
    /// </summary>
    public static void RegisterDomainEventOutboxGauges(IDomainEventOutboxHealthTracker tracker)
    {
        if (_domainEventOutboxGaugesRegistered)
        {
            return;
        }
        _domainEventOutboxGaugesRegistered = true;

        Meter.CreateObservableGauge(
            name: "meepleai.domain_event_outbox.pending.count",
            observeValue: () => tracker.GetPendingCount(),
            unit: "rows",
            description: "Number of domain_event_outbox rows currently in Pending status.");

        Meter.CreateObservableGauge(
            name: "meepleai.domain_event_outbox.pending.oldest_age_seconds",
            observeValue: () => tracker.GetOldestPendingAgeSeconds(),
            unit: "s",
            description: "Age in seconds of the oldest Pending domain_event_outbox row (0 when the queue is empty).");

        Meter.CreateObservableGauge(
            name: "meepleai.domain_event_outbox.failed.count",
            observeValue: () => tracker.GetFailedCount(),
            unit: "rows",
            description: "Number of domain_event_outbox rows currently in Failed status (terminal, awaiting operator intervention).");
    }

    /// <summary>
    /// Test-only reset hook so suites that exercise
    /// <see cref="RegisterDomainEventOutboxGauges"/> can be made idempotent across multiple
    /// fixture instantiations. NOT for production use.
    /// </summary>
    internal static void ResetDomainEventOutboxGaugesForTest()
    {
        _domainEventOutboxGaugesRegistered = false;
    }
}

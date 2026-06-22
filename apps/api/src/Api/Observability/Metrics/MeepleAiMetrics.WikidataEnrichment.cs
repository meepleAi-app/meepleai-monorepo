// #1823 Phase B — Wikidata enrichment metrics scaffolding per ADR DEC-3g
using System.Diagnostics.Metrics;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    /// <summary>
    /// Latest QID hit-rate observed by a batch run (0.0–1.0). Updated by the
    /// future <c>EnrichCatalogCoverCommandHandler</c> at the end of each pass.
    /// Spike sess.46h measured 0.60 on a 30-game sample; production expected
    /// ~0.50 weighted by catalog distribution.
    /// </summary>
    /// <remarks>
    /// Updated via <see cref="SetWikidataQidHitRate(double)"/> rather than a
    /// pull-callback so callers can record exactly the rate produced by a
    /// concrete batch. ObservableGauge would otherwise re-poll on each scrape
    /// and risk reporting stale values across batch boundaries.
    /// </remarks>
    private static double _wikidataQidHitRate;

    /// <summary>
    /// Counter of enrichment attempts grouped by terminal <c>outcome</c> tag
    /// (<c>success</c>, <c>failure</c>, <c>dead_letter</c>). Increment in the
    /// future <c>EnrichCatalogCoverCommandHandler</c> after each per-game
    /// terminal outcome.
    ///
    /// Suggested alerting:
    ///   - dead_letter rate &gt; 5% sustained for &gt; 1 batch → license whitelist or
    ///     SPARQL drift; spike re-verify.
    ///   - failure rate &gt; 30% sustained for &gt; 30 min → trip circuit breaker
    ///     manually; check Wikidata + Commons endpoint health.
    /// </summary>
    public static readonly Counter<long> WikidataEnrichmentAttempts = Meter.CreateCounter<long>(
        name: "meepleai.wikidata.enrichment.attempts.total",
        unit: "attempts",
        description: "Wikidata cover enrichment attempts per terminal outcome (#1823 DEC-3g)");

    /// <summary>
    /// Histogram of Wikidata SPARQL endpoint latency in seconds. Used by ops
    /// to detect endpoint degradation that should trip the circuit breaker
    /// (DEC-3f). Buckets aligned to typical SPARQL response distribution.
    ///
    /// Suggested alerting:
    ///   - p99 latency &gt; 10s sustained for &gt; 5 min → endpoint degraded; pre-trip
    ///     circuit breaker.
    ///   - rate of latency observations drops to 0 during scheduled batch window →
    ///     circuit breaker already OPEN; investigate.
    /// </summary>
    public static readonly Histogram<double> WikidataSparqlLatency = Meter.CreateHistogram<double>(
        name: "meepleai.wikidata.sparql.latency_seconds",
        unit: "s",
        description: "Wikidata SPARQL endpoint round-trip latency (#1823 DEC-3g)");

    /// <summary>
    /// Observable gauge reporting the last-batch QID hit-rate (0.0–1.0). Pull
    /// callback returns the value last set via
    /// <see cref="SetWikidataQidHitRate(double)"/>.
    ///
    /// Suggested alerting:
    ///   - value drops &gt; 10pp below 30-day rolling average → Wikidata schema
    ///     drift or catalog quality change; investigate.
    ///   - value &lt; 0.25 for two consecutive batches → spike threshold breach;
    ///     reconsider Phase D quarterly re-verification cadence.
    /// </summary>
    public static readonly ObservableGauge<double> WikidataQidHitRate = Meter.CreateObservableGauge(
        name: "meepleai.wikidata.qid_hit_rate",
        observeValue: () => _wikidataQidHitRate,
        unit: "ratio",
        description: "Last-batch Wikidata QID hit-rate, 0.0-1.0 (#1823 DEC-3g)");

    /// <summary>
    /// Updates the value reported by <see cref="WikidataQidHitRate"/>. Callers
    /// MUST pass a value in [0.0, 1.0]; out-of-range inputs are clamped.
    /// </summary>
    public static void SetWikidataQidHitRate(double rate)
    {
        _wikidataQidHitRate = rate switch
        {
            < 0.0 => 0.0,
            > 1.0 => 1.0,
            _ => rate
        };
    }

    /// <summary>
    /// Issue #1823 Wave 3 M11 — last-batch queue depth (#games due for
    /// enrichment as picked up by the M9 scheduler tick). Updated via
    /// <see cref="SetWikidataQueueDepth(int)"/> rather than a pull callback so
    /// ops dashboards reflect the most recent batch sizing decision.
    ///
    /// Suggested alerting:
    ///   - depth &gt; 5000 sustained for &gt; 1 hour → backlog building, scheduler
    ///     under-provisioned relative to enrichment rate; consider tuning
    ///     batch size or rate-limit.
    ///   - depth = 0 for &gt; 4 hours during a known-active batch window →
    ///     scheduler not picking up games; investigate Quartz health or DEC-3e
    ///     rate-limiter starvation.
    /// </summary>
    private static int _wikidataQueueDepth;

    public static readonly ObservableGauge<int> WikidataQueueDepth = Meter.CreateObservableGauge(
        name: "meepleai.wikidata.queue_depth",
        observeValue: () => _wikidataQueueDepth,
        unit: "games",
        description: "Last-batch queue depth: #SharedGames due for Wikidata cover enrichment, picked up by the M9 scheduler tick (#1823 Wave 3 M11)");

    /// <summary>
    /// Updates the value reported by <see cref="WikidataQueueDepth"/>. Clamps
    /// negative inputs to 0 (a depth cannot be negative).
    /// </summary>
    public static void SetWikidataQueueDepth(int depth)
    {
        _wikidataQueueDepth = depth < 0 ? 0 : depth;
    }

    /// <summary>
    /// Issue #1823 Wave 3 M11 — wall-clock duration of a single
    /// <c>WikidataCoverEnrichmentJob.RunBatchAsync</c> tick, in seconds.
    /// Recorded once per tick regardless of how many games were processed
    /// (including zero-due ticks).
    ///
    /// Suggested alerting:
    ///   - p95 &gt; 90s sustained → batch is overrunning the 60s trigger interval;
    ///     missed ticks risk piling up. Reduce batch size or lower throttle.
    ///   - p99 &gt; 600s → individual game stuck on a service call; investigate
    ///     DEC-3e rate-limiter contention or circuit-breaker hot-loop.
    /// </summary>
    public static readonly Histogram<double> WikidataBatchDuration = Meter.CreateHistogram<double>(
        name: "meepleai.wikidata.batch_duration_seconds",
        unit: "s",
        description: "WikidataCoverEnrichmentJob tick wall-clock duration (#1823 Wave 3 M11)");

    /// <summary>
    /// Issue #1823 Wave 3 F1 (M11 follow-up) — cumulative count of
    /// <c>WikidataCoverEnrichmentAttempt</c> rows in <c>DeadLetter</c> state
    /// currently present in the table (i.e. NOT yet swept by the
    /// <c>WikidataCoverDeadLetterRetentionJob</c>). Hybrid update strategy:
    /// the retention job calls <see cref="SetWikidataDeadLetterCount(int)"/>
    /// with a fresh repo COUNT after each sweep; the
    /// <c>WikidataCoverEnrichmentRunner</c> calls
    /// <see cref="IncrementWikidataDeadLetterCount"/> whenever it persists a
    /// new dead-letter attempt. Drift between sweeps is bounded by the
    /// 1-minute scheduler tick rate; the daily 03:00 UTC sweep re-anchors
    /// the value to ground truth.
    ///
    /// Suggested alerting:
    ///   - count &gt; 100 sustained &gt; 1 hour → operator triage backlog
    ///     building; investigate via M13 admin dead-letter page.
    ///   - sudden jump &gt; 50 in &lt; 5 min → systemic upstream failure or
    ///     license-whitelist drift; cross-check WikidataSparqlLatency p95.
    /// </summary>
    private static int _wikidataDeadLetterCount;

    public static readonly ObservableGauge<int> WikidataDeadLetterCount = Meter.CreateObservableGauge(
        name: "meepleai.wikidata.dead_letter_count",
        observeValue: () => _wikidataDeadLetterCount,
        unit: "attempts",
        description: "Cumulative count of dead-letter WikidataCoverEnrichmentAttempt rows (#1823 Wave 3 F1)");

    /// <summary>
    /// Re-anchors the gauge to a freshly-counted ground-truth value.
    /// Uses <see cref="System.Threading.Interlocked.Exchange(ref int, int)"/>
    /// so the re-anchor write is ordered atomically against concurrent
    /// <see cref="IncrementWikidataDeadLetterCount"/> calls from the runner
    /// (ARM64 memory model: a plain assignment could otherwise be reordered
    /// past a subsequent Interlocked.Increment, producing a transient stale
    /// gauge value).
    /// </summary>
    public static void SetWikidataDeadLetterCount(int count)
    {
        System.Threading.Interlocked.Exchange(ref _wikidataDeadLetterCount, count < 0 ? 0 : count);
    }

    /// <summary>Atomically increments the gauge by 1 — call after persisting a new dead-letter attempt.</summary>
    public static void IncrementWikidataDeadLetterCount()
    {
        System.Threading.Interlocked.Increment(ref _wikidataDeadLetterCount);
    }

    /// <summary>
    /// Issue #2055 Phase G AC-G1 — counter of rows reset to <c>NULL</c> by the
    /// quarterly re-verification cron (<see cref="Api.BoundedContexts.SharedGameCatalog.Application.Jobs.WikidataQuarterlyReVerificationJob"/>).
    /// Incremented once per cron tick by the reset count returned from
    /// <c>RunOnceAsync</c>. Zero-tick increments are tracked separately by
    /// <c>WikidataBatchDuration</c>'s sample count on the M9 batch job — this
    /// counter only fires when the quarterly sweep actually reset something.
    ///
    /// Suggested alerting:
    ///   - sudden surge &gt; 1000 rows in a single tick → schema drift or migration
    ///     pushed an entire cohort past the 90d window; verify M9 throughput can
    ///     absorb the backlog on its next tick.
    ///   - zero resets across 4 consecutive quarterly ticks (1 year) → either
    ///     catalog is stable (good) or the freshness clock is stuck (bad);
    ///     cross-check with <c>WikidataQidHitRate</c>.
    /// </summary>
    public static readonly Counter<long> WikidataQuarterlyReverificationReset = Meter.CreateCounter<long>(
        name: "meepleai.wikidata.quarterly_reverification.reset.total",
        unit: "games",
        description: "Cumulative count of SharedGames reset to NULL by WikidataQuarterlyReVerificationJob (#2055 Phase G AC-G1)");

    // ──────────────────────────────────────────────────────────────────────
    // Issue #2470 — SSE plane observability (Phase E F4 + #2256 backplane)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Callback supplied by the hosted gauge binder that resolves the live
    /// subscriber count from <c>IWikidataEnrichmentEventBroadcaster</c>.
    /// Null until <see cref="SetWikidataSseSubscribersCallback(Func{int})"/>
    /// is invoked at host startup; the gauge reports 0 while unbound so a
    /// missing binder fails open instead of crashing the metric scrape.
    /// </summary>
    private static Func<int>? _wikidataSseSubscribersCallback;

    /// <summary>
    /// Issue #2470 — current SSE subscriber count on this pod. Sourced from
    /// the broadcaster's <c>SubscriberCount</c> diagnostic via a lazy
    /// callback set by the hosted binder. Per the issue DEC-A, the
    /// <c>WikidataSseSubscriberStarvation</c> alert (see
    /// <c>infra/prometheus/alerts/wikidata-sse.yml</c>) correlates this
    /// gauge with <see cref="WikidataSseAdminClientsConnected"/> to fire only
    /// when an admin is actually waiting for live updates.
    /// </summary>
    public static readonly ObservableGauge<int> WikidataSseSubscribers = Meter.CreateObservableGauge(
        name: "meepleai.wikidata.sse.subscribers",
        observeValue: () =>
        {
            try
            {
                return _wikidataSseSubscribersCallback?.Invoke() ?? 0;
            }
#pragma warning disable CA1031 // Metric scrapes must never throw — degrade to 0.
            catch
#pragma warning restore CA1031
            {
                return 0;
            }
        },
        unit: "subscribers",
        description: "Current SSE subscriber count on this pod (#2470)");

    /// <summary>
    /// Binds the gauge callback. Intended to be called once by
    /// <c>WikidataSseGaugeBinder</c> at host startup; subsequent calls
    /// replace the callback atomically (test helpers rely on this to swap
    /// in deterministic values).
    /// </summary>
    public static void SetWikidataSseSubscribersCallback(Func<int> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        System.Threading.Interlocked.Exchange(ref _wikidataSseSubscribersCallback, callback);
    }

    /// <summary>
    /// Test-only reset of the callback. Production code MUST NOT call this —
    /// the gauge will revert to reporting 0 until a new callback is set.
    /// </summary>
    internal static void ResetWikidataSseSubscribersCallback()
    {
        System.Threading.Interlocked.Exchange(ref _wikidataSseSubscribersCallback, null);
    }

    /// <summary>
    /// Callback supplied by the hosted gauge binder that resolves the live
    /// admin-clients-connected count from
    /// <c>WikidataAdminClientHeartbeatTracker</c>. Same lazy-init semantics
    /// as <see cref="_wikidataSseSubscribersCallback"/>.
    /// </summary>
    private static Func<int>? _wikidataSseAdminClientsConnectedCallback;

    /// <summary>
    /// Issue #2470 (DEC-C) — count of distinct admin sessions that have
    /// posted a heartbeat in the last 90 seconds, indicating the admin
    /// dead-letter page is open and expecting live updates. Used by the
    /// alert correlation: a starvation alert only fires when an admin is
    /// actively watching.
    /// </summary>
    public static readonly ObservableGauge<int> WikidataSseAdminClientsConnected = Meter.CreateObservableGauge(
        name: "meepleai.wikidata.sse.admin_clients_connected",
        observeValue: () =>
        {
            try
            {
                return _wikidataSseAdminClientsConnectedCallback?.Invoke() ?? 0;
            }
#pragma warning disable CA1031
            catch
#pragma warning restore CA1031
            {
                return 0;
            }
        },
        unit: "clients",
        description: "Distinct admin sessions heartbeating the SSE dead-letter page in the last 90s (#2470 DEC-C)");

    /// <summary>Binds the admin-clients gauge callback. Same semantics as <see cref="SetWikidataSseSubscribersCallback(Func{int})"/>.</summary>
    public static void SetWikidataSseAdminClientsConnectedCallback(Func<int> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        System.Threading.Interlocked.Exchange(ref _wikidataSseAdminClientsConnectedCallback, callback);
    }

    /// <summary>Test-only reset; mirror of <see cref="ResetWikidataSseSubscribersCallback"/>.</summary>
    internal static void ResetWikidataSseAdminClientsConnectedCallback()
    {
        System.Threading.Interlocked.Exchange(ref _wikidataSseAdminClientsConnectedCallback, null);
    }

    /// <summary>
    /// Issue #2470 — counter incremented once per <c>Publish</c> call on the
    /// broadcaster (both in-memory and Redis variants). Mirrors the
    /// publisher-side activity rate so an operator can spot "publishes flowing
    /// but no subscribers" via Grafana panel 10.
    /// </summary>
    public static readonly Counter<long> WikidataSseMessagesPublished = Meter.CreateCounter<long>(
        name: "meepleai.wikidata.sse.messages.published.total",
        unit: "messages",
        description: "Wikidata SSE attempt-recorded messages emitted by the publisher (#2470)");

    /// <summary>
    /// Issue #2470 — counter incremented once per per-subscriber delivery on
    /// this pod. For the in-memory broadcaster this is +N (N = local
    /// subscribers) per <c>Publish</c>; for the Redis broadcaster it is +N
    /// per incoming Redis pub/sub message. Diverges from
    /// <see cref="WikidataSseMessagesPublished"/> only when a multi-pod
    /// deployment fans the same publish out to subscribers on remote pods.
    /// </summary>
    public static readonly Counter<long> WikidataSseMessagesReceived = Meter.CreateCounter<long>(
        name: "meepleai.wikidata.sse.messages.received.total",
        unit: "messages",
        description: "Wikidata SSE messages delivered to local subscriber channels (#2470)");
}

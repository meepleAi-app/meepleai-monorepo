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
}

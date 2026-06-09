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
}

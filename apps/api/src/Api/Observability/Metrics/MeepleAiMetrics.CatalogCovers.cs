// Issue #2123 — BGG ToS compliance: catalog cover observability metrics.
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    /// <summary>
    /// Counter of cover-URL resolutions tagged by the source layer that won.
    /// Emitted by <c>CoverUrlResolver</c> on every public/user resolution. Label
    /// <c>source</c> follows the L3→L4→L2.5→L2→null priority chain:
    /// <list type="bullet">
    ///   <item><c>r2_user</c>: user-uploaded custom cover (L3)</item>
    ///   <item><c>r2_pdf</c>: PDF-derived cover from rulebook page 1 (L4)</item>
    ///   <item><c>r2_bgg</c>: BGG cover re-uploaded server-side via the admin pipeline (L2.5)</item>
    ///   <item><c>r2_wikidata</c>: Wikidata/Wikimedia Commons cover (L2)</item>
    ///   <item><c>r2_manual</c>: admin manual-URL cover re-hosted on R2 (epic #3470); only
    ///     wins as an explicit per-context admin override, never in the implicit chain</item>
    ///   <item><c>placeholder</c>: no cover available, FE renders deterministic placeholder</item>
    /// </list>
    ///
    /// Suggested alerting:
    /// <list type="bullet">
    ///   <item><c>rate(meepleai_cover_resolution_total{source="placeholder"}[5m]) / rate(meepleai_cover_resolution_total[5m]) &gt; 0.80</c>
    ///     sustained for &gt; 15min → covers pipeline degradation; the bootstrap
    ///     QID+M8 batch needs to run, or upstream Wikimedia is failing.</item>
    /// </list>
    /// </summary>
    public static readonly Counter<long> CoverResolution = Meter.CreateCounter<long>(
        name: "meepleai.cover.resolution.total",
        unit: "resolutions",
        description: "Cover URL resolution outcomes grouped by source layer (#2123)");

    /// <summary>
    /// SLO-zero counter: any nonzero increment indicates that a browser
    /// attempted to render an image whose hostname matches the BGG block list
    /// (<c>cf.geekdo-images.com</c>, <c>geekdo-images.com</c>,
    /// <c>images.geekdo.com</c>, <c>*.boardgamegeek.com</c>). The custom
    /// Next.js image loader caught it and redirected to the placeholder, but
    /// the attempt itself MUST be investigated — it means a code path is
    /// passing a BGG URL into <c>&lt;Image&gt;</c> instead of the runtime-resolved
    /// <c>SharedGameDto.CoverUrl</c>.
    ///
    /// Suggested alerting:
    /// <list type="bullet">
    ///   <item><c>rate(meepleai_bgg_url_attempted_render_total[5m]) &gt; 0</c> at any
    ///     time → P1 ToS-violation attempt; see operations runbook
    ///     <c>docs/for-developers/operations/operations-manual.md</c>
    ///     § "Catalog covers — BGG ToS compliance".</item>
    /// </list>
    ///
    /// Label <c>path</c>: the FE route that issued the offending request, used
    /// by ops to localise the offending component.
    /// </summary>
    public static readonly Counter<long> BggUrlAttemptedRender = Meter.CreateCounter<long>(
        name: "meepleai.bgg.url.attempted_render.total",
        unit: "attempts",
        description: "SLO=0: browser attempts to render a BGG-hosted asset (#2123)");

    /// <summary>Wire value for the <c>outcome</c> tag: cover generated successfully.</summary>
    public const string CoverGenerationOutcomeGenerated = "generated";

    /// <summary>Wire value for the <c>outcome</c> tag: generation skipped (heuristic rejected pages).</summary>
    public const string CoverGenerationOutcomeSkipped = "skipped";

    /// <summary>Wire value for the <c>outcome</c> tag: generation failed TERMINALLY (retry budget exhausted, or a permanent extractor failure).</summary>
    public const string CoverGenerationOutcomeFailed = "failed";

    /// <summary>
    /// Wire value for the <c>outcome</c> tag: a TRANSIENT generation failure that stays retry-eligible
    /// (returned to <c>Pending</c> with attempts still below <c>PdfCoverRetryPolicy.MaxAttempts</c>; #3373 D1).
    /// Kept distinct from <see cref="CoverGenerationOutcomeFailed"/> so the degradation alert on
    /// <c>outcome="failed"</c> tracks only terminal failures — a self-healing infra blip must not inflate it.
    /// </summary>
    public const string CoverGenerationOutcomeRetrying = "retrying";

    /// <summary>
    /// Counter of PDF cover-GENERATION outcomes (as opposed to <see cref="CoverResolution"/>,
    /// which is read-time). Emitted by the three PDF cover producers —
    /// <c>PdfProcessingPipelineService</c> (inline ingest), <c>BackfillPdfCoversJob</c> and
    /// <c>MaterializePdfCoverCommandHandler</c> — so ops can see generation health
    /// (generated/skipped/failed rate) directly instead of waiting for the lagging
    /// read-time <c>placeholder&gt;80%</c> signal. Tags: <c>source</c> (currently <c>pdf</c>),
    /// <c>outcome</c> (<c>generated</c>|<c>skipped</c>|<c>failed</c>|<c>retrying</c>). Decision D5-C, umbrella #3373.
    ///
    /// Suggested alerting:
    /// <list type="bullet">
    ///   <item><c>rate(meepleai_cover_generation_total{outcome="failed"}[15m]) / rate(meepleai_cover_generation_total[15m]) &gt; 0.20</c>
    ///     sustained → PDF cover generation is degrading (extraction, R2 upload or DB save
    ///     failing); investigate before the read-time placeholder&gt;80% alert fires.
    ///     Post-#3373 D1 <c>outcome="failed"</c> counts only TERMINAL failures; a transient
    ///     blip that self-heals within the retry budget is tagged <c>retrying</c> instead, so
    ///     this ratio stays diagnostic rather than firing on recoverable infra hiccups.</item>
    /// </list>
    /// </summary>
    public static readonly Counter<long> CoverGeneration = Meter.CreateCounter<long>(
        name: "meepleai.cover.generation.total",
        unit: "generations",
        description: "PDF cover generation outcomes grouped by source + outcome (#3373 D5-C)");

    /// <summary>
    /// Records one PDF cover-generation outcome. <paramref name="outcome"/> must be one of
    /// <see cref="CoverGenerationOutcomeGenerated"/> / <see cref="CoverGenerationOutcomeSkipped"/> /
    /// <see cref="CoverGenerationOutcomeFailed"/> / <see cref="CoverGenerationOutcomeRetrying"/>.
    /// </summary>
    public static void RecordPdfCoverGeneration(string outcome) =>
        CoverGeneration.Add(
            1,
            new KeyValuePair<string, object?>("source", "pdf"),
            new KeyValuePair<string, object?>("outcome", outcome));
}

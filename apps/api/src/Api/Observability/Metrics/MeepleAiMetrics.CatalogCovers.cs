// Issue #2123 — BGG ToS compliance: catalog cover observability metrics.
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
}

namespace Api.BoundedContexts.DocumentProcessing.Domain.Services;

/// <summary>
/// Table-region router (#3435 SP2, decision DC-B). Decides whether a PDF is a candidate for the
/// Metà-C VLM enrichment pass from its <b>image-region count</b> — the number of persisted
/// <c>Image</c>/<c>FigureCaption</c> regions in <c>pdf_image_regions</c> (already area-filtered
/// ≥3% by #3456). A PDF with at least <see cref="DefaultMinImageRegions"/> such regions is treated
/// as "table-heavy" and gates the (expensive) VLM into running only on it (NFR1: cost isolation —
/// never on text-only PDFs).
/// </summary>
/// <remarks>
/// This mirrors the static-decider shape of <see cref="ExtractionStrategyDecider"/> but deliberately
/// does NOT reuse its <c>Table</c> predicate: that predicate reads the extractor's own table count,
/// which was inert on the sampled corpus (0 Tables across 52 PDFs — spec §1), whereas hi_res reliably
/// emits bbox-bearing <c>Image</c>/<c>FigureCaption</c> elements for the same table graphics.
/// #3565 refined that premise: hi_res DOES occasionally emit a bbox-bearing <c>Table</c> element
/// (1 in 699 on wingspan — the scorecard), and those are now seeded as regions too. The router is
/// unaffected because it counts rows in <c>pdf_image_regions</c> without looking at
/// <c>ElementType</c> — a <c>Table</c> region simply counts toward candidacy like any other.
/// Rejected alternatives (spec §5quinquies):
/// a <c>has_tables</c> VLM sample (would couple the router to the blocked Metà-C — the router must
/// GATE the VLM, not depend on it) and a per-game manual flag (toil across 52 games, does not scale).
/// The threshold is the single tunable knob; the query handler may override the MVP default of
/// <see cref="DefaultMinImageRegions"/> from config so a too-permissive/too-strict corpus can be
/// retuned without a redeploy.
/// </remarks>
public static class TableRegionCandidateDecider
{
    /// <summary>MVP threshold: a single area-filtered image region already flags a PDF as a candidate.</summary>
    public const int DefaultMinImageRegions = 1;

    /// <summary>
    /// Normalizes a requested threshold to the effective floor: a non-positive value clamps to 1 (a
    /// candidate always needs ≥1 region — a 0-region PDF is never a table-heavy candidate), so a
    /// misconfigured <c>0</c>/negative value can't flag the entire corpus. The query handler calls this
    /// before building its SQL <c>HAVING count(*) &gt;= threshold</c> so the SQL predicate stays in
    /// lockstep with <see cref="IsCandidate"/> — the clamp lives here once, not duplicated at the call site.
    /// </summary>
    public static int NormalizeThreshold(int minImageRegions) => minImageRegions < 1 ? 1 : minImageRegions;

    /// <summary>
    /// True when <paramref name="imageRegionCount"/> reaches the normalized
    /// <paramref name="minImageRegions"/> (see <see cref="NormalizeThreshold"/>).
    /// </summary>
    public static bool IsCandidate(int imageRegionCount, int minImageRegions = DefaultMinImageRegions)
        => imageRegionCount >= NormalizeThreshold(minImageRegions);
}

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetCorpusTitleHealth;

/// <summary>
/// Epic #3338 WP3: the per-game extraction-quality read-out exposed by
/// <c>GET /api/v1/admin/kb/title-health</c> — one row per shared game that has chunked headings.
/// Computed by <see cref="Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking.TitleHealthMetric"/>
/// over the game's distinct <c>text_chunks.Heading</c> values (what retrieval sees).
/// </summary>
/// <param name="GameId">The <c>shared_games.id</c> the chunks are scoped to.</param>
/// <param name="GameTitle">The shared game's title (for the read-out / baseline label).</param>
/// <param name="Language">The effective language (<c>LanguageOverride ?? Language</c>) labelling the most distinct headings for this game (retrieval-surface-weighted majority, not the per-PDF count).</param>
/// <param name="DistinctHeadings">Distinct non-blank headings across the game's chunks.</param>
/// <param name="PlausibleHeadings">How many of those look like real section titles.</param>
/// <param name="PlausibleFraction">PlausibleHeadings / DistinctHeadings, rounded to 4 dp for a stable baseline (0 when there are none).</param>
/// <param name="CanonicalCoverage">Distinct plausible headings matching a curated lexicon section word.</param>
/// <param name="Band">green ≥ 0.80 · yellow ≥ 0.50 · red otherwise — the WP4 go/no-go band.</param>
public sealed record GameTitleHealthDto(
    Guid GameId,
    string GameTitle,
    string? Language,
    int DistinctHeadings,
    int PlausibleHeadings,
    double PlausibleFraction,
    int CanonicalCoverage,
    string Band);

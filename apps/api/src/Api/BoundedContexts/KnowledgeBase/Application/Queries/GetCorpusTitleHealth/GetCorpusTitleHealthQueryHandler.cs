using Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetCorpusTitleHealth;

/// <summary>
/// Handles <see cref="GetCorpusTitleHealthQuery"/>.
///
/// <para>Pulls the DISTINCT <c>(game, effective-language, heading)</c> triples the corpus carries
/// — joining <c>text_chunks</c> → <c>shared_games</c> (title) → <c>pdf_documents</c> (language) — then
/// groups by game and runs the pure <see cref="TitleHealthMetric"/> over each game's headings. The
/// DISTINCT is pushed to the DB so child chunks repeating their parent heading do not inflate the
/// transfer; <see cref="TitleHealthMetric.Compute"/> dedups again defensively.</para>
///
/// <para>Only games with at least one non-blank heading appear — a game with zero chunked headings has
/// no extraction-quality signal to report. Ordered by title (ordinal) for a deterministic baseline.</para>
/// </summary>
internal sealed class GetCorpusTitleHealthQueryHandler
    : IQueryHandler<GetCorpusTitleHealthQuery, IReadOnlyList<GameTitleHealthDto>>
{
    private const int FractionPrecision = 4;

    private readonly MeepleAiDbContext _db;

    public GetCorpusTitleHealthQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<IReadOnlyList<GameTitleHealthDto>> Handle(
        GetCorpusTitleHealthQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Correlated Where instead of an explicit join keeps the nullable GameId (Guid?) lift clean;
        // both projections are inner (a chunk with an orphaned GameId / PdfDocumentId is dropped).
        var rows = await (
            from tc in _db.TextChunks.AsNoTracking()
            // Exclude blank headings at the DB (btrim(heading) <> '') so a game whose chunks carry only
            // null/empty/whitespace headings forms no group and is absent — it has no extraction signal.
            // A real (even garbage) heading like "D" is NOT blank and correctly yields a red-band game.
            where tc.GameId != null && tc.Heading != null && tc.Heading.Trim() != ""
            from sg in _db.SharedGames.AsNoTracking().Where(g => g.Id == tc.GameId)
            from pd in _db.PdfDocuments.AsNoTracking().Where(p => p.Id == tc.PdfDocumentId)
            select new HeadingRow
            {
                GameId = sg.Id,
                Title = sg.Title,
                Language = pd.LanguageOverride ?? pd.Language,
                Heading = tc.Heading!,
            })
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .GroupBy(r => new { r.GameId, r.Title })
            .Select(g =>
            {
                var health = TitleHealthMetric.Compute(g.Select(x => x.Heading));

                // The effective language labelling the MOST distinct headings for this game (rows are
                // already Distinct on (game,lang,heading), so this weights the retrieval surface, not the
                // PDF count); ordinal tiebreak keeps a tie stable.
                var language = g
                    .GroupBy(x => x.Language, StringComparer.Ordinal)
                    .OrderByDescending(lg => lg.Count())
                    .ThenBy(lg => lg.Key, StringComparer.Ordinal)
                    .First().Key;

                return new GameTitleHealthDto(
                    g.Key.GameId,
                    g.Key.Title,
                    language,
                    health.DistinctHeadings,
                    health.PlausibleHeadings,
                    Math.Round(health.PlausibleFraction, FractionPrecision, MidpointRounding.AwayFromZero),
                    health.CanonicalCoverage,
                    health.Band);
            })
            .OrderBy(d => d.GameTitle, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>DB projection row — a named type (not anonymous) so EF's DISTINCT is stable and the group key is explicit.</summary>
    private sealed record HeadingRow
    {
        public Guid GameId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string? Language { get; init; }
        public string Heading { get; init; } = string.Empty;
    }
}

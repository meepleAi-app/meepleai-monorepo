using Api.Services;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Parallel orchestration wrapper for cross-game hybrid search.
///
/// Strategy (R3 verdict b):
/// 1. Early-exit when gameIds is empty (EC-1).
/// 2. Launch one <see cref="IHybridSearchService.SearchAsync"/> task per game (parallel via Task.WhenAll).
/// 3. Per-game exceptions are caught and logged as warnings; the query continues with the remaining
///    games (EC-2 / EC-7 resilience: a game with no indexed content should not abort the whole search).
/// 4. Per-game already-fused results are tagged with their origin gameId and aggregated.
/// 5. Apply minScore filter.
/// 6. Sort by HybridScore DESC, ChunkIndex ASC, PdfDocumentId ASC (EC-4 stable ordering).
/// 7. Take the requested limit (hard cap, EC-7).
///
/// <b>No second RRF pass</b>: scores returned by <see cref="IHybridSearchService"/> are already RRF-fused
/// within each game and share the same scale. A sort-by-score achieves rank-equivalent ordering
/// without the double-normalisation distortion of applying RRF again on fused inputs.
/// </summary>
internal sealed class MultiGameHybridSearchService : IMultiGameHybridSearchService
{
    private readonly IHybridSearchService _hybridSearch;
    private readonly ILogger<MultiGameHybridSearchService> _logger;

    public MultiGameHybridSearchService(
        IHybridSearchService hybridSearch,
        ILogger<MultiGameHybridSearchService> logger)
    {
        _hybridSearch = hybridSearch;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MultiGameSearchResultItem>> SearchAsync(
        string query,
        IReadOnlyList<Guid> gameIds,
        int limit,
        SearchMode mode = SearchMode.Hybrid,
        double minScore = 0.0,
        CancellationToken cancellationToken = default)
    {
        // EC-1: no accessible games → return empty immediately
        if (gameIds.Count == 0)
            return Array.Empty<MultiGameSearchResultItem>();

        _logger.LogInformation(
            "MultiGameHybridSearch: query='{Query}', gameCount={GameCount}, limit={Limit}, mode={Mode}, minScore={MinScore}",
            query, gameIds.Count, limit, mode, minScore);

        // Step 1: Launch parallel per-game searches.
        // We request 'limit' results per game so each game contributes enough candidates
        // before the cross-game sort + truncation.  A cap of min(limit, 50) is applied to
        // prevent per-game over-fetching when limit is very large.
        var perGameLimit = Math.Min(Math.Max(limit, 1), 50);

        var tasks = gameIds
            .Select(gameId => SearchGameSafeAsync(query, gameId, perGameLimit, mode, cancellationToken))
            .ToList();

        // Task.WhenAll guarantees parallel execution (all tasks start before any is awaited).
        var perGameResultArrays = await Task.WhenAll(tasks).ConfigureAwait(false);

        // Step 2: Aggregate, project to MultiGameSearchResultItem (preserving origin gameId).
        // perGameResultArrays is (GameId, List<HybridSearchResult>)[] — we flatten while keeping gameId.
        var aggregated = perGameResultArrays
            .SelectMany(tuple => tuple.Results, (tuple, r) => ProjectItem(tuple.GameId, r))
            .ToList();

        _logger.LogInformation(
            "MultiGameHybridSearch aggregated {TotalRaw} results before minScore={MinScore} filter",
            aggregated.Count, minScore);

        // Step 3: Apply minScore filter.
        if (minScore > 0.0)
            aggregated = aggregated.Where(r => r.HybridScore >= (float)minScore).ToList();

        // Step 4: Deterministic ordering (EC-4): score DESC, then ChunkIndex ASC, then PdfDocumentId ASC.
        aggregated.Sort(static (a, b) =>
        {
            var scoreCmp = b.HybridScore.CompareTo(a.HybridScore); // DESC
            if (scoreCmp != 0) return scoreCmp;

            var chunkCmp = a.ChunkIndex.CompareTo(b.ChunkIndex);   // ASC
            if (chunkCmp != 0) return chunkCmp;

            return string.Compare(a.PdfDocumentId, b.PdfDocumentId, StringComparison.Ordinal); // ASC
        });

        // Step 5: Hard-cap at limit (EC-7).
        if (aggregated.Count > limit)
            aggregated = aggregated.Take(limit).ToList();

        _logger.LogInformation(
            "MultiGameHybridSearch completed: returning {ResultCount} results",
            aggregated.Count);

        return aggregated;
    }

    /// <summary>
    /// Issues a single-game search, catching and logging any exception so that
    /// one failing game does not abort the entire cross-game query (EC-2 resilience).
    /// </summary>
    private async Task<(Guid GameId, List<HybridSearchResult> Results)> SearchGameSafeAsync(
        string query,
        Guid gameId,
        int limit,
        SearchMode mode,
        CancellationToken cancellationToken)
    {
        try
        {
            var results = await _hybridSearch.SearchAsync(
                query,
                gameId,
                mode,
                limit,
                documentIds: null,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return (gameId, results);
        }
#pragma warning disable CA1031 // Resilience: per-game exception must not abort cross-game query
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "MultiGameHybridSearch: search failed for gameId={GameId}, skipping game. Query='{Query}'",
                gameId, query);
            return (gameId, new List<HybridSearchResult>());
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Projects a per-game <see cref="HybridSearchResult"/> to a <see cref="MultiGameSearchResultItem"/>,
    /// using the explicitly passed <paramref name="gameId"/> (the origin game from the parallel task)
    /// rather than <c>r.GameId</c> which may be the query-default rather than the actual game.
    /// </summary>
    private static MultiGameSearchResultItem ProjectItem(Guid gameId, HybridSearchResult r) =>
        new()
        {
            GameId = gameId,
            ChunkId = r.ChunkId,
            PdfDocumentId = r.PdfDocumentId,
            ChunkIndex = r.ChunkIndex,
            PageNumber = r.PageNumber,
            Content = r.Content,
            HybridScore = r.HybridScore,
            VectorScore = r.VectorScore,
            KeywordScore = r.KeywordScore,
            MatchedTerms = r.MatchedTerms,
            Mode = r.Mode
        };
}

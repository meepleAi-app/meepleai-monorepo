using Api.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Parallel orchestration wrapper for cross-game hybrid search.
///
/// Strategy (R3 verdict b):
/// 1. Early-exit when gameIds is empty (EC-1).
/// 2. Launch one <see cref="IHybridSearchService.SearchAsync"/> task per game. Each task runs in its
///    OWN DI scope so it gets its OWN <c>MeepleAiDbContext</c> — the per-game services are scoped and
///    a single request-scoped DbContext is NOT safe for concurrent use ("A second operation was
///    started on this context instance…"), which previously made every cross-game search throw and
///    return 0 results (#2480). A <see cref="SemaphoreSlim"/> caps concurrency so a user with many
///    accessible games (e.g. an admin) cannot exhaust the connection pool.
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
    // Cap concurrent per-game searches. Each acquires its own DI scope (own DbContext +
    // DB connection), so the bound also bounds the connection-pool draw when a user has
    // many accessible games.
    private const int MaxConcurrentGameSearches = 4;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MultiGameHybridSearchService> _logger;

    public MultiGameHybridSearchService(
        IServiceScopeFactory scopeFactory,
        ILogger<MultiGameHybridSearchService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MultiGameSearchResultItem>> SearchAsync(
        string query,
        IReadOnlyList<Guid> gameIds,
        int limit,
        SearchMode mode = SearchMode.Hybrid,
        double minScore = 0.0,
        IReadOnlyList<Guid>? documentIds = null,
        CancellationToken cancellationToken = default)
    {
        // EC-1: no accessible games → return empty immediately
        if (gameIds.Count == 0)
            return Array.Empty<MultiGameSearchResultItem>();

        _logger.LogInformation(
            "MultiGameHybridSearch: query='{Query}', gameCount={GameCount}, limit={Limit}, mode={Mode}, minScore={MinScore}, documentIdsCount={DocCount}",
            query, gameIds.Count, limit, mode, minScore, documentIds?.Count);

        // Convert IReadOnlyList → List once (interface requires List<Guid>? on the per-game service).
        // Materialise outside the per-game loop so all parallel calls share the same instance.
        var documentIdsList = documentIds is null ? null : new List<Guid>(documentIds);

        // Step 1: Launch parallel per-game searches.
        // We request 'limit' results per game so each game contributes enough candidates
        // before the cross-game sort + truncation.  A cap of min(limit, 50) is applied to
        // prevent per-game over-fetching when limit is very large.
        var perGameLimit = Math.Min(Math.Max(limit, 1), 50);

        using var throttle = new SemaphoreSlim(MaxConcurrentGameSearches);
        var tasks = gameIds
            .Select(gameId => SearchGameSafeAsync(query, gameId, perGameLimit, mode, documentIdsList, throttle, cancellationToken))
            .ToList();

        // Task.WhenAll guarantees parallel execution (all tasks start before any is awaited);
        // the semaphore inside each task bounds how many run the DB work concurrently.
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
    /// Issues a single-game search in its OWN DI scope (own DbContext), catching and logging any
    /// exception so that one failing game does not abort the entire cross-game query (EC-2 resilience).
    /// The <paramref name="throttle"/> bounds how many per-game searches hit the DB concurrently.
    /// The <paramref name="documentIds"/> allowlist (Issue #1686) restricts the per-game hit set to a
    /// subset of PDF documents; <c>null</c> = no document filter.
    /// </summary>
    private async Task<(Guid GameId, List<HybridSearchResult> Results)> SearchGameSafeAsync(
        string query,
        Guid gameId,
        int limit,
        SearchMode mode,
        List<Guid>? documentIds,
        SemaphoreSlim throttle,
        CancellationToken cancellationToken)
    {
        await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Own scope → own scoped MeepleAiDbContext, so concurrent per-game searches never
            // share a DbContext instance (the root cause of the cross-game "second operation on
            // this context" failures that returned 0 results, #2480).
            using var scope = _scopeFactory.CreateScope();
            var hybridSearch = scope.ServiceProvider.GetRequiredService<IHybridSearchService>();

            var results = await hybridSearch.SearchAsync(
                query,
                gameId,
                mode,
                limit,
                documentIds: documentIds,
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
        finally
        {
            throttle.Release();
        }
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

using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Helpers;
using Api.Infrastructure;
using Microsoft.Extensions.Options;
using KbEntities = Api.BoundedContexts.KnowledgeBase.Domain.Entities;

#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
namespace Api.Services;

/// <summary>
/// Hybrid search service combining vector similarity (Qdrant) with keyword matching (PostgreSQL).
/// Implements Reciprocal Rank Fusion (RRF) algorithm for score merging.
/// Part of AI-14 implementation.
/// </summary>
internal class HybridSearchService : IHybridSearchService
{
    private readonly IKeywordSearchService _keywordSearchService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStoreAdapter _vectorStore;
    private readonly ILogger<HybridSearchService> _logger;
    private readonly HybridSearchConfiguration _config;

    // RRF constant k (standard value from research papers: Cormack et al. 2009)
    // Formula: RRF_score = sum(1 / (k + rank_i))
    // Higher k gives less weight to rank differences, k=60 is empirically optimal
    private const int DefaultRrfK = 60;

    public HybridSearchService(
        IKeywordSearchService keywordSearchService,
        IEmbeddingService embeddingService,
        IVectorStoreAdapter vectorStore,
        ILogger<HybridSearchService> logger,
        IOptions<HybridSearchConfiguration> config)
    {
        _keywordSearchService = keywordSearchService;
        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
        _logger = logger;
        _config = config.Value;
    }

    public async Task<List<HybridSearchResult>> SearchAsync(
        string query,
        Guid gameId,
        SearchMode mode = SearchMode.Hybrid,
        int limit = 10,
        List<Guid>? documentIds = null,
        float vectorWeight = 0.7f,
        float keywordWeight = 0.3f,
        CancellationToken cancellationToken = default)
    {
        // Issue #1445: Use centralized query validation
        var queryError = QueryValidator.ValidateQuery(query);
        if (queryError != null)
        {
            _logger.LogWarning("Invalid query provided to HybridSearchService: {Error}", queryError);
            // Return empty results for invalid queries (maintains existing behavior)
            return new List<HybridSearchResult>();
        }

        // Security: Cap limit parameter to prevent resource exhaustion
        var safeLimit = Math.Min(Math.Max(limit, 1), 100); // Min: 1, Max: 100

        _logger.LogInformation(
            "Hybrid search started: query='{Query}', gameId={GameId}, mode={Mode}, documentFilter={HasFilter}, vectorWeight={VectorWeight}, keywordWeight={KeywordWeight}, limit={Limit}",
            query, gameId, mode, documentIds != null, vectorWeight, keywordWeight, limit);

        try
        {
            switch (mode)
            {
                case SearchMode.Semantic:
                    return await SearchSemanticOnlyAsync(query, gameId, safeLimit, documentIds, cancellationToken).ConfigureAwait(false);

                case SearchMode.Keyword:
                    return await SearchKeywordOnlyAsync(query, gameId, safeLimit, documentIds, cancellationToken).ConfigureAwait(false);

                case SearchMode.Hybrid:
                    return await SearchHybridAsync(query, gameId, safeLimit, vectorWeight, keywordWeight, documentIds, cancellationToken).ConfigureAwait(false);

                default:
                    throw new ArgumentException($"Unsupported search mode: {mode}", nameof(mode));
            }
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // SERVICE BOUNDARY: Coordinates vector (Qdrant) and keyword (PostgreSQL) search with centralized exception logging
#pragma warning restore S125
        catch (Exception ex)
        {
            // Issue #1444: Use centralized exception handling (log and re-throw pattern)
            // Service entry point that coordinates vector and keyword searches
            RagExceptionHandler.LogAndRethrow(ex, _logger, "hybrid search", query, mode);
            throw; // Unreachable, but required for compiler
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    /// <summary>
    /// Performs vector-only semantic search using pgvector.
    /// </summary>
    private async Task<List<HybridSearchResult>> SearchSemanticOnlyAsync(
        string query,
        Guid gameId,
        int limit,
        List<Guid>? documentIds,
        CancellationToken cancellationToken)
    {
        var vectorResults = await ExecuteVectorSearchAsync(
            query, gameId, limit, documentIds, cancellationToken).ConfigureAwait(false);

        return vectorResults.Select((r, index) => new HybridSearchResult
        {
            ChunkId = $"{r.VectorDocumentId}_{r.ChunkIndex}",
            Content = r.TextContent,
            PdfDocumentId = r.VectorDocumentId.ToString(),
            GameId = gameId,
            ChunkIndex = r.ChunkIndex,
            PageNumber = r.PageNumber,
            HybridScore = 1.0f / (index + 1), // normalized rank score
            VectorScore = 1.0f / (index + 1),
            KeywordScore = null,
            VectorRank = index + 1,
            KeywordRank = null,
            MatchedTerms = new List<string>(),
            Mode = SearchMode.Semantic
        }).ToList();
    }

    /// <summary>
    /// Performs keyword-only search using PostgreSQL full-text search.
    /// </summary>
    private async Task<List<HybridSearchResult>> SearchKeywordOnlyAsync(
        string query,
        Guid gameId,
        int limit,
        List<Guid>? documentIds,
        CancellationToken cancellationToken)
    {
        var keywordResults = await _keywordSearchService.SearchAsync(
            query,
            gameId,
            limit,
            phraseSearch: query.Contains('"'), // Enable phrase search if query has quotes
            boostTerms: _config.BoostTerms,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        // Issue #2051: Filter by document IDs if specified
        var filteredResults = documentIds == null
            ? keywordResults
            : keywordResults.Where(r => documentIds.Any(id => string.Equals(id.ToString(), r.PdfDocumentId, StringComparison.Ordinal))).ToList();

        _logger.LogInformation(
            "Keyword search: {TotalResults} results from PostgreSQL, {FilteredResults} after document filter",
            keywordResults.Count, filteredResults.Count);

        return filteredResults.Select((r, index) => new HybridSearchResult
        {
            ChunkId = r.ChunkId,
            Content = r.Content,
            PdfDocumentId = r.PdfDocumentId,
            GameId = r.GameId,
            ChunkIndex = r.ChunkIndex,
            PageNumber = r.PageNumber,
            HybridScore = r.RelevanceScore, // Use keyword score directly as hybrid score
            VectorScore = null,
            KeywordScore = r.RelevanceScore,
            VectorRank = null,
            KeywordRank = index + 1,
            MatchedTerms = r.MatchedTerms,
            Mode = SearchMode.Keyword
        }).ToList();
    }

    /// <summary>
    /// Performs hybrid search combining pgvector semantic and keyword results with RRF fusion.
    /// Vector and keyword searches run in parallel for optimal latency.
    /// </summary>
    private async Task<List<HybridSearchResult>> SearchHybridAsync(
        string query,
        Guid gameId,
        int limit,
        float vectorWeight,
        float keywordWeight,
        List<Guid>? documentIds,
        CancellationToken cancellationToken)
    {
        var fetchLimit = Math.Max(limit * 2, 20);

        // Run vector and keyword searches in parallel
        var vectorTask = ExecuteVectorSearchAsync(
            query, gameId, fetchLimit, documentIds, cancellationToken);

        var keywordTask = _keywordSearchService.SearchAsync(
            query,
            gameId,
            fetchLimit,
            phraseSearch: query.Contains('"'),
            boostTerms: _config.BoostTerms,
            cancellationToken: cancellationToken);

        await Task.WhenAll(vectorTask, keywordTask).ConfigureAwait(false);

        var vectorEmbeddings = await vectorTask.ConfigureAwait(false);
        var keywordResults = await keywordTask.ConfigureAwait(false);

        // Apply document filter to keyword results
        var filteredKeywordResults = documentIds == null
            ? keywordResults
            : keywordResults.Where(r => documentIds.Any(id =>
                string.Equals(id.ToString(), r.PdfDocumentId, StringComparison.Ordinal))).ToList();

        // Convert pgvector results to SearchResultItem for RRF fusion
        var vectorItems = vectorEmbeddings.Select(e => new SearchResultItem
        {
            Score = 1.0f,
            Text = e.TextContent,
            PdfId = e.VectorDocumentId.ToString(),
            ChunkIndex = e.ChunkIndex,
            Page = e.PageNumber
        }).ToArray();

        _logger.LogInformation(
            "Hybrid search: vectorCount={VectorCount}, keywordCount={KeywordCount} (post-filter: {FilteredKeyword})",
            vectorItems.Length, keywordResults.Count, filteredKeywordResults.Count);

        // RRF fusion with both vector AND keyword results
        var fusedResults = FuseSearchResults(
            vectorItems,
            filteredKeywordResults,
            gameId,
            vectorWeight,
            keywordWeight,
            _config.RrfConstant ?? DefaultRrfK);

        var topResults = fusedResults
            .OrderByDescending(r => r.HybridScore)
            .Take(limit)
            .ToList();

        _logger.LogInformation(
            "Hybrid search completed: returned {ResultCount} fused results (from {TotalFused} total)",
            topResults.Count, fusedResults.Count);

        return topResults;
    }

    /// <summary>
    /// Generates query embedding and performs pgvector cosine similarity search.
    /// Falls back to empty results if embedding generation or search fails (graceful degradation).
    /// </summary>
    private async Task<List<KbEntities.Embedding>> ExecuteVectorSearchAsync(
        string query,
        Guid gameId,
        int limit,
        List<Guid>? documentIds,
        CancellationToken cancellationToken)
    {
        try
        {
            var embeddingResult = await _embeddingService
                .GenerateEmbeddingAsync(query, cancellationToken)
                .ConfigureAwait(false);

            if (!embeddingResult.Success || embeddingResult.Embeddings is not { Count: > 0 })
            {
                _logger.LogWarning(
                    "Query embedding generation failed: {Error}. Falling back to keyword-only.",
                    embeddingResult.ErrorMessage);
                return new List<KbEntities.Embedding>();
            }

            var queryVector = new Vector(embeddingResult.Embeddings[0]);

            var results = await _vectorStore.SearchAsync(
                gameId,
                queryVector,
                topK: limit,
                minScore: 0.3,
                documentIds: documentIds,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "pgvector search returned {Count} results for gameId={GameId}",
                results.Count, gameId);

            return results;
        }
#pragma warning disable CA1031 // Graceful degradation: vector search failure must not break hybrid search
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Vector search failed, falling back to keyword-only for gameId={GameId}",
                gameId);
            return new List<KbEntities.Embedding>();
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Fuses vector and keyword search results using Reciprocal Rank Fusion (RRF).
    /// RRF formula: score = sum_over_all_rankings(weight / (k + rank))
    /// where k=60 (empirically optimal constant), rank is 1-based position.
    /// </summary>
    /// <remarks>
    /// RRF advantages:
    /// - No score normalization needed (works with heterogeneous scoring systems)
    /// - Emphasizes top-ranked results from both systems
    /// - Robust to score scale differences between vector (0-1) and keyword (unbounded) scores
    /// Reference: Cormack et al. "Reciprocal Rank Fusion outperforms Condorcet and individual rank learning methods" (SIGIR 2009)
    /// </remarks>
    private List<HybridSearchResult> FuseSearchResults(
        IReadOnlyList<SearchResultItem> vectorResults,
        IReadOnlyList<KeywordSearchResult> keywordResults,
        Guid gameId,
        float vectorWeight,
        float keywordWeight,
        int rrfK)
    {
        // Build lookup dictionaries for O(1) access by composite chunk ID (PdfId_ChunkIndex)
        var vectorLookup = vectorResults
            .Select((r, index) => new
            {
                ChunkId = $"{r.PdfId}_{r.ChunkIndex}", // Composite key
                Result = r,
                Rank = index + 1 // 1-based ranking
            })
            .ToDictionary(x => x.ChunkId, StringComparer.Ordinal);

        var keywordLookup = keywordResults
            .Select((r, index) => new { ChunkId = r.ChunkId, Result = r, Rank = index + 1 })
            .ToDictionary(x => x.ChunkId, StringComparer.Ordinal);

        // Collect all unique chunk IDs from both result sets
        var allChunkIds = vectorLookup.Keys
            .Union(keywordLookup.Keys, StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        _logger.LogDebug(
            "Fusing results: {VectorCount} vector, {KeywordCount} keyword, {UniqueCount} unique chunks",
            vectorResults.Count, keywordResults.Count, allChunkIds.Count);

        // Calculate RRF score for each chunk
        var fusedResults = new List<HybridSearchResult>();

        foreach (var chunkId in allChunkIds)
        {
            var hasVector = vectorLookup.TryGetValue(chunkId, out var vectorItem);
            var hasKeyword = keywordLookup.TryGetValue(chunkId, out var keywordItem);

            // Calculate RRF score components
            float vectorRrfScore = 0f;
            float keywordRrfScore = 0f;

            if (hasVector)
            {
                vectorRrfScore = vectorItem != null ? vectorWeight / (rrfK + vectorItem.Rank) : 0f;
            }

            if (hasKeyword)
            {
                keywordRrfScore = keywordItem != null ? keywordWeight / (rrfK + keywordItem.Rank) : 0f;
            }

            var hybridScore = vectorRrfScore + keywordRrfScore;

            // Use data from whichever result has it (prefer vector for metadata consistency)
            var matchedTerms = hasKeyword && keywordItem != null
                ? keywordItem.Result.MatchedTerms
                : new List<string>();

            // At least one of vectorItem or keywordItem must exist since we got the chunkId from their union
            var content = hasVector && vectorItem != null
                ? vectorItem.Result.Text
                : (keywordItem?.Result.Content ?? string.Empty);

            var pdfDocumentId = hasVector && vectorItem != null
                ? vectorItem.Result.PdfId
                : (keywordItem?.Result.PdfDocumentId ?? string.Empty);

            var chunkGameId = hasKeyword && keywordItem != null
                ? keywordItem.Result.GameId
                : gameId; // Use from keyword or fall back to query gameId

            var chunkIndex = hasVector && vectorItem != null
                ? vectorItem.Result.ChunkIndex
                : (keywordItem?.Result.ChunkIndex ?? 0);

            var pageNumber = hasVector && vectorItem != null
                ? vectorItem.Result.Page
                : (keywordItem?.Result.PageNumber ?? 0);

            fusedResults.Add(new HybridSearchResult
            {
                ChunkId = chunkId,
                Content = content,
                PdfDocumentId = pdfDocumentId,
                GameId = chunkGameId,
                ChunkIndex = chunkIndex,
                PageNumber = pageNumber,
                HybridScore = hybridScore,
                VectorScore = hasVector && vectorItem != null ? vectorItem.Result.Score : null,
                KeywordScore = hasKeyword && keywordItem != null ? keywordItem.Result.RelevanceScore : null,
                VectorRank = hasVector && vectorItem != null ? vectorItem.Rank : null,
                KeywordRank = hasKeyword && keywordItem != null ? keywordItem.Rank : null,
                MatchedTerms = matchedTerms,
                Mode = SearchMode.Hybrid
            });
        }

        return fusedResults;
    }
}

/// <summary>
/// Configuration for hybrid search.
/// Loaded from appsettings.json HybridSearch section.
/// </summary>
internal class HybridSearchConfiguration
{
    /// <summary>
    /// Weight for vector search results (default: 0.7).
    /// Higher weight emphasizes semantic similarity.
    /// </summary>
    public float VectorWeight { get; set; } = 0.7f;

    /// <summary>
    /// Weight for keyword search results (default: 0.3).
    /// Higher weight emphasizes exact term matching.
    /// </summary>
    public float KeywordWeight { get; set; } = 0.3f;

    /// <summary>
    /// RRF constant k (default: 60).
    /// Higher k reduces impact of rank differences.
    /// Standard value from research: 60 (Cormack et al. 2009).
    /// </summary>
    public int? RrfConstant { get; set; } = 60;

    /// <summary>
    /// Game-specific terms to boost in keyword search.
    /// Examples: "castling", "en passant", "check", "checkmate"
    /// </summary>
    public List<string> BoostTerms { get; set; } = new List<string>();
}
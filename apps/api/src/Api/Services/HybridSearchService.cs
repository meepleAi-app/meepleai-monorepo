using System.Text.RegularExpressions;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Helpers;
using Api.Infrastructure;
using Microsoft.Extensions.Options;
using KbEntities = Api.BoundedContexts.KnowledgeBase.Domain.Entities;

#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
namespace Api.Services;

/// <summary>
/// Hybrid search service combining vector similarity (pgvector) with keyword matching (PostgreSQL FTS).
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

    // Phase D (D6): additive boost applied to chunks whose RoleTags overlap with the user
    // intent's QueryRoleHint. Chosen ~10x the smallest expected RRF contribution (≈ 0.7/(60+10) ≈ 0.01)
    // so a single matching tag clearly outranks a non-matching peer at the same retrieval rank,
    // but does not steamroll strong vector+keyword agreement at the top.
    internal const float RoleMatchBoost = 0.15f;

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
        double keywordMinScore = 0.0,
        GameBookRole queryRoleHint = GameBookRole.None,
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
            "Hybrid search started: query='{Query}', gameId={GameId}, mode={Mode}, documentFilter={HasFilter}, vectorWeight={VectorWeight}, keywordWeight={KeywordWeight}, limit={Limit}, roleHint={RoleHint}",
            query, gameId, mode, documentIds != null, vectorWeight, keywordWeight, limit, queryRoleHint);

        try
        {
            switch (mode)
            {
                case SearchMode.Semantic:
                    return await SearchSemanticOnlyAsync(query, gameId, safeLimit, documentIds, queryRoleHint, cancellationToken).ConfigureAwait(false);

                case SearchMode.Keyword:
                    return await SearchKeywordOnlyAsync(query, gameId, safeLimit, documentIds, keywordMinScore, queryRoleHint, cancellationToken).ConfigureAwait(false);

                case SearchMode.Hybrid:
                    return await SearchHybridAsync(query, gameId, safeLimit, vectorWeight, keywordWeight, documentIds, keywordMinScore, queryRoleHint, cancellationToken).ConfigureAwait(false);

                default:
                    throw new ArgumentException($"Unsupported search mode: {mode}", nameof(mode));
            }
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // SERVICE BOUNDARY: Coordinates vector (pgvector) and keyword (PostgreSQL FTS) search with centralized exception logging
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
    /// Issue #1391: applies the same role-match boost as the keyword path when
    /// <paramref name="queryRoleHint"/> overlaps the chunk's denormalized RoleTags
    /// (pgvector_embeddings now carries role_tags via the AddRoleTagsToPgVectorEmbeddings
    /// migration + PgVectorStoreAdapter ingestion writes). Re-sorts by HybridScore so
    /// boosted chunks float to the top instead of staying anchored to the raw vector rank.
    /// </summary>
    private async Task<List<HybridSearchResult>> SearchSemanticOnlyAsync(
        string query,
        Guid gameId,
        int limit,
        List<Guid>? documentIds,
        GameBookRole queryRoleHint,
        CancellationToken cancellationToken)
    {
        var vectorResults = await ExecuteVectorSearchAsync(
            query, gameId, limit, documentIds, cancellationToken).ConfigureAwait(false);

        var results = vectorResults.Select((r, index) =>
        {
            var embedding = r.Embedding;
            var chunkRoleTags = (GameBookRole)embedding.RoleTags;
            var baseScore = 1.0f / (index + 1); // normalized rank score
            var roleBoost = ComputeRoleMatchBoost(queryRoleHint, chunkRoleTags);
            return new HybridSearchResult
            {
                ChunkId = $"{embedding.VectorDocumentId}_{embedding.ChunkIndex}",
                Content = embedding.TextContent,
                PdfDocumentId = embedding.VectorDocumentId.ToString(),
                GameId = gameId,
                ChunkIndex = embedding.ChunkIndex,
                PageNumber = embedding.PageNumber,
                // HybridScore stays the rank-based base (+ role boost) so within-game ordering
                // is unchanged. VectorScore carries the RAW cosine (#2568) so the cross-game
                // merge can break rank-only ties by true query relevance.
                HybridScore = baseScore + roleBoost,
                VectorScore = (float)r.Score,
                KeywordScore = null,
                VectorRank = index + 1,
                KeywordRank = null,
                MatchedTerms = new List<string>(),
                Mode = SearchMode.Semantic,
                RoleTags = chunkRoleTags
            };
        }).ToList();

        // Re-sort if the role boost actually moved anything (no-op when queryRoleHint == None).
        if (queryRoleHint != GameBookRole.None)
        {
            results = results
                .OrderByDescending(r => r.HybridScore)
                .ToList();
        }

        return results;
    }

    /// <summary>
    /// Performs keyword-only search using PostgreSQL full-text search.
    /// Phase D (D6): applies the role-match boost on top of the raw ts_rank_cd score when
    /// <paramref name="queryRoleHint"/> overlaps a chunk's RoleTags.
    /// </summary>
    private async Task<List<HybridSearchResult>> SearchKeywordOnlyAsync(
        string query,
        Guid gameId,
        int limit,
        List<Guid>? documentIds,
        double keywordMinScore,
        GameBookRole queryRoleHint,
        CancellationToken cancellationToken)
    {
        var keywordResults = await _keywordSearchService.SearchAsync(
            query,
            gameId,
            limit,
            phraseSearch: query.Contains('"'), // Enable phrase search if query has quotes
            boostTerms: _config.BoostTerms,
            minScore: keywordMinScore,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        // Issue #2051: Filter by document IDs if specified
        var filteredResults = documentIds == null
            ? keywordResults
            : keywordResults.Where(r => documentIds.Any(id => string.Equals(id.ToString(), r.PdfDocumentId, StringComparison.Ordinal))).ToList();

        _logger.LogInformation(
            "Keyword search: {TotalResults} results from PostgreSQL, {FilteredResults} after document filter",
            keywordResults.Count, filteredResults.Count);

        // Phase D (D6): build hybrid results then re-rank by role-boosted score.
        var hybridResults = filteredResults.Select((r, index) =>
        {
            var roleBoost = ComputeRoleMatchBoost(queryRoleHint, r.RoleTags);
            return new HybridSearchResult
            {
                ChunkId = r.ChunkId,
                Content = r.Content,
                PdfDocumentId = r.PdfDocumentId,
                GameId = r.GameId,
                ChunkIndex = r.ChunkIndex,
                PageNumber = r.PageNumber,
                HybridScore = r.RelevanceScore + roleBoost, // role-aware re-ranking
                VectorScore = null,
                KeywordScore = r.RelevanceScore,
                VectorRank = null,
                KeywordRank = index + 1,
                MatchedTerms = r.MatchedTerms,
                Mode = SearchMode.Keyword,
                RoleTags = r.RoleTags
            };
        }).ToList();

        // Re-sort if the role boost actually moved anything (no-op when queryRoleHint == None).
        if (queryRoleHint != GameBookRole.None)
        {
            hybridResults = hybridResults
                .OrderByDescending(r => r.HybridScore)
                .ToList();
        }

        return hybridResults;
    }

    /// <summary>
    /// Performs hybrid search combining pgvector semantic and keyword results with RRF fusion.
    /// Vector and keyword searches run in parallel for optimal latency.
    /// Phase D (D6): role-match boost is applied per chunk during fusion when <paramref name="queryRoleHint"/> is not None.
    /// </summary>
    private async Task<List<HybridSearchResult>> SearchHybridAsync(
        string query,
        Guid gameId,
        int limit,
        float vectorWeight,
        float keywordWeight,
        List<Guid>? documentIds,
        double keywordMinScore,
        GameBookRole queryRoleHint,
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
            minScore: keywordMinScore,
            cancellationToken: cancellationToken);

        await Task.WhenAll(vectorTask, keywordTask).ConfigureAwait(false);

        var vectorEmbeddings = await vectorTask.ConfigureAwait(false);
        var keywordResults = await keywordTask.ConfigureAwait(false);

        // Apply document filter to keyword results
        var filteredKeywordResults = documentIds == null
            ? keywordResults
            : keywordResults.Where(r => documentIds.Any(id =>
                string.Equals(id.ToString(), r.PdfDocumentId, StringComparison.Ordinal))).ToList();

        // Convert pgvector results to SearchResultItem for RRF fusion.
        // #2568: carry the raw cosine similarity (was hard-coded 1.0f) so the fused
        // VectorScore reflects true relevance for the cross-game tiebreak. RRF still ranks
        // by list position (Rank), so this does NOT change within-game ordering.
        var vectorItems = vectorEmbeddings.Select(se => new SearchResultItem
        {
            Score = (float)se.Score,
            Text = se.Embedding.TextContent,
            // RRF fusion-key fix: key on the owning PdfDocumentId (resolved by the scored pgvector
            // search) so a chunk found by BOTH arms fuses on the same {PdfDocumentId}_{ChunkIndex}
            // identity the keyword arm uses. (Was VectorDocumentId, which also wrongly surfaced as
            // HybridSearchResult.PdfDocumentId for vector-only citations.)
            PdfId = se.Embedding.PdfDocumentId.ToString(),
            ChunkIndex = se.Embedding.ChunkIndex,
            Page = se.Embedding.PageNumber,
            // Slice C: carry role_tags through so vector-only chunks get the role-match boost in
            // fusion (same cast as the semantic-only path). pgvector already SELECTs role_tags.
            RoleTags = (GameBookRole)se.Embedding.RoleTags
        }).ToArray();

        _logger.LogInformation(
            "Hybrid search: vectorCount={VectorCount}, keywordCount={KeywordCount} (post-filter: {FilteredKeyword})",
            vectorItems.Length, keywordResults.Count, filteredKeywordResults.Count);

        // RRF fusion with both vector AND keyword results
        // Phase D (D6): queryRoleHint enables role-match boost during fusion.
        var fusedResults = FuseSearchResults(
            vectorItems,
            filteredKeywordResults,
            gameId,
            vectorWeight,
            keywordWeight,
            _config.RrfConstant ?? DefaultRrfK,
            queryRoleHint);

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
    private async Task<List<KbEntities.ScoredEmbedding>> ExecuteVectorSearchAsync(
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
                return new List<KbEntities.ScoredEmbedding>();
            }

            var queryVector = new Vector(embeddingResult.Embeddings[0]);

            // #2568: use the scored variant so the raw cosine similarity is preserved on each
            // hit. The cross-game merge (MultiGameHybridSearchService) needs a globally-
            // comparable signal to break rank-only RRF ties; the cosine is that signal.
            // Same SQL / ordering / minScore as SearchAsync — additive method (#1653).
            var results = await _vectorStore.SearchWithScoresAsync(
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
            return new List<KbEntities.ScoredEmbedding>();
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
        int rrfK,
        GameBookRole queryRoleHint)
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

        // RRF fusion-key fix: key the keyword arm on the SAME {PdfDocumentId}_{ChunkIndex} composite
        // as the vector arm (was the raw text_chunks.Id, which never matched the vector key — so a
        // doubly-retrieved chunk was emitted as two half-strength duplicates instead of being fused).
        var keywordLookup = keywordResults
            .Select((r, index) => new { ChunkId = $"{r.PdfDocumentId}_{r.ChunkIndex}", Result = r, Rank = index + 1 })
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

            // Phase D (D6) + Slice C: role-match boost. Take the role tags from whichever arm
            // surfaced the chunk (both denormalize the same text_chunks.role_tags — keyword from
            // text_chunks, vector from pgvector_embeddings.role_tags). The union is defensive: with
            // the current fusion keying the two arms never share a key (vector = "{VectorDocumentId}
            // _{ChunkIndex}", keyword = raw text_chunks.Id), so each entry is single-arm and the OR
            // reduces to the present arm — a genuine both-arm merge awaits the fusion-key alignment
            // follow-up. When the chunk overlaps the user's classified intent, apply an additive
            // boost on top of the fused RRF.
            var vectorRoleTags = hasVector && vectorItem != null
                ? vectorItem.Result.RoleTags
                : GameBookRole.None;
            var keywordRoleTags = hasKeyword && keywordItem != null
                ? keywordItem.Result.RoleTags
                : GameBookRole.None;
            var chunkRoleTags = vectorRoleTags | keywordRoleTags;
            var roleBoost = ComputeRoleMatchBoost(queryRoleHint, chunkRoleTags);

            // Use data from whichever result has it (prefer vector for metadata consistency)
            var matchedTerms = hasKeyword && keywordItem != null
                ? keywordItem.Result.MatchedTerms
                : new List<string>();

            // At least one of vectorItem or keywordItem must exist since we got the chunkId from their union
            var content = hasVector && vectorItem != null
                ? vectorItem.Result.Text
                : (keywordItem?.Result.Content ?? string.Empty);

            // RAG answer-quality fix: demote cross-reference "legend" chunks (dense with
            // "vedi pag."/"see p." pointers, e.g. the Terraforming Mars component list
            // "8. Progetti Standard ... vedi pag. 10 ... 9. Milestone ... vedi pag. 10 e 11")
            // which carry no actionable answer yet score in the same RRF band as real content.
            // The demotion scales the RRF fusion components only; the role-match boost (#1391)
            // stays strictly additive on top so its deliberate ~0.15 calibration is not scaled away.
            var legendFactor = ComputeLegendPenaltyFactor(content);
            var hybridScore = ((vectorRrfScore + keywordRrfScore) * (1f - legendFactor)) + roleBoost;

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
                Mode = SearchMode.Hybrid,
                RoleTags = chunkRoleTags
            });
        }

        return fusedResults;
    }

    /// <summary>
    /// Phase D (D6): computes the additive RRF score boost for a chunk based on role overlap.
    /// Returns <see cref="RoleMatchBoost"/> when <paramref name="queryRoleHint"/> intersects
    /// <paramref name="chunkRoleTags"/> (both being non-<see cref="GameBookRole.None"/>); otherwise 0.
    /// Pure function — exposed as <c>internal static</c> for direct unit testing of the re-ranker
    /// without spinning up pgvector + PostgreSQL FTS infrastructure.
    /// </summary>
    internal static float ComputeRoleMatchBoost(GameBookRole queryRoleHint, GameBookRole chunkRoleTags)
    {
        if (queryRoleHint == GameBookRole.None || chunkRoleTags == GameBookRole.None)
        {
            return 0f;
        }

        return (chunkRoleTags & queryRoleHint) != GameBookRole.None ? RoleMatchBoost : 0f;
    }

    // Matches cross-reference pointers like "vedi pag. 10", "vedi anche pagina 5", "see p. 11",
    // "cfr. pagg. 4-5". Allows an optional connector word ("anche"/"a") and spelled-out "pagina".
    private static readonly Regex CrossReferencePointer = new(
        @"(?i)\b(?:vedi|see|cfr|cf)\b\.?\s+(?:anche\s+|a\s+)?(?:pagine|pagina|pagg|pag|pages|page|pp|p)\b\.?",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// RAG answer-quality fix: computes a multiplicative demotion factor in [0, 0.5] for chunks
    /// that are cross-reference "legends" (dense with "vedi pag."/"see p." pointers, e.g. a
    /// component list that only points elsewhere). Such chunks carry no actionable content but
    /// score in the same RRF band as real answers. Pure function — exposed <c>internal static</c>
    /// for direct unit testing.
    /// </summary>
    internal static float ComputeLegendPenaltyFactor(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return 0f;
        }

        var pointers = CrossReferencePointer.Count(content);
        // A legend is a LIST of pointers; a single incidental page reference is not one.
        if (pointers < 2)
        {
            return 0f;
        }

        // Density-driven ONLY (pointers per 1000 chars), never the raw count: a short chunk that
        // is mostly pointers (a legend) has high density and is capped at 0.5, while a long,
        // substantive section that legitimately cites several pages has low density and is barely
        // touched. Using the raw count would over-demote long real content with many references.
        var density = pointers * 1000.0 / content.Length;
        return (float)Math.Min(0.5, 0.05 * density);
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
using Api.Infrastructure;
using Microsoft.Extensions.Options;

namespace Api.Services;

/// <summary>
/// Hybrid search service combining vector similarity (Qdrant) with keyword matching (PostgreSQL).
/// Implements Reciprocal Rank Fusion (RRF) algorithm for score merging.
/// Part of AI-14 implementation.
/// </summary>
public class HybridSearchService : IHybridSearchService
{
    private readonly IQdrantService _qdrantService;
    private readonly IKeywordSearchService _keywordSearchService;
    private readonly IEmbeddingService _embeddingService;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<HybridSearchService> _logger;
    private readonly HybridSearchConfiguration _config;

    // RRF constant k (standard value from research papers: Cormack et al. 2009)
    // Formula: RRF_score = sum(1 / (k + rank_i))
    // Higher k gives less weight to rank differences, k=60 is empirically optimal
    private const int DefaultRrfK = 60;

    public HybridSearchService(
        IQdrantService qdrantService,
        IKeywordSearchService keywordSearchService,
        IEmbeddingService embeddingService,
        MeepleAiDbContext dbContext,
        ILogger<HybridSearchService> logger,
        IOptions<HybridSearchConfiguration> config)
    {
        _qdrantService = qdrantService;
        _keywordSearchService = keywordSearchService;
        _embeddingService = embeddingService;
        _dbContext = dbContext;
        _logger = logger;
        _config = config.Value;
    }

    public async Task<List<HybridSearchResult>> SearchAsync(
        string query,
        Guid gameId,
        SearchMode mode = SearchMode.Hybrid,
        int limit = 10,
        float vectorWeight = 0.7f,
        float keywordWeight = 0.3f,
        CancellationToken cancellationToken = default)
    {
        // Security: Validate query length to prevent DoS attacks
        if (query?.Length > 2000)
        {
            _logger.LogWarning("Query exceeds maximum length: {Length} characters", query.Length);
            throw new ArgumentException("Query exceeds maximum length of 2000 characters", nameof(query));
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            _logger.LogWarning("Empty query provided to HybridSearchService");
            return new List<HybridSearchResult>();
        }

        // Security: Cap limit parameter to prevent resource exhaustion
        var safeLimit = Math.Min(Math.Max(limit, 1), 100); // Min: 1, Max: 100

        _logger.LogInformation(
            "Hybrid search started: query='{Query}', gameId={GameId}, mode={Mode}, vectorWeight={VectorWeight}, keywordWeight={KeywordWeight}, limit={Limit}",
            query, gameId, mode, vectorWeight, keywordWeight, limit);

        try
        {
            switch (mode)
            {
                case SearchMode.Semantic:
                    return await SearchSemanticOnlyAsync(query, gameId, safeLimit, cancellationToken);

                case SearchMode.Keyword:
                    return await SearchKeywordOnlyAsync(query, gameId, safeLimit, cancellationToken);

                case SearchMode.Hybrid:
                    return await SearchHybridAsync(query, gameId, safeLimit, vectorWeight, keywordWeight, cancellationToken);

                default:
                    throw new ArgumentException($"Unsupported search mode: {mode}");
            }
        }
        catch (Exception ex)
        {
            // Service layer: Logs exception details before re-throwing
            // Caller receives exception with full diagnostic context
            _logger.LogError(ex, "Error during hybrid search for query '{Query}', mode={Mode}", query, mode);
            throw;
        }
    }

    /// <summary>
    /// Performs vector-only semantic search using Qdrant.
    /// </summary>
    private async Task<List<HybridSearchResult>> SearchSemanticOnlyAsync(
        string query,
        Guid gameId,
        int limit,
        CancellationToken cancellationToken)
    {
        // Generate embedding for the query
        var embeddingResult = await _embeddingService.GenerateEmbeddingAsync(query, "en", cancellationToken);
        if (!embeddingResult.Success || embeddingResult.Embeddings.Count == 0)
        {
            _logger.LogError("Failed to generate query embedding for semantic search: {Error}", embeddingResult.ErrorMessage);
            return new List<HybridSearchResult>();
        }

        var queryEmbedding = embeddingResult.Embeddings[0];

        // Search Qdrant with embedding
        var vectorResults = await _qdrantService.SearchAsync(
            gameId.ToString(),
            queryEmbedding,
            limit: limit,
            ct: cancellationToken);

        if (!vectorResults.Success)
        {
            _logger.LogError("Qdrant search failed: {Error}", vectorResults.ErrorMessage);
            return new List<HybridSearchResult>();
        }

        return vectorResults.Results.Select((r, index) => new HybridSearchResult
        {
            ChunkId = $"{r.PdfId}_{r.ChunkIndex}", // Composite key for chunk identification
            Content = r.Text,
            PdfDocumentId = r.PdfId,
            GameId = gameId,
            ChunkIndex = r.ChunkIndex,
            PageNumber = r.Page,
            HybridScore = r.Score, // Use vector score directly as hybrid score
            VectorScore = r.Score,
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
        CancellationToken cancellationToken)
    {
        var keywordResults = await _keywordSearchService.SearchAsync(
            query,
            gameId,
            limit,
            phraseSearch: query.Contains("\""), // Enable phrase search if query has quotes
            boostTerms: _config.BoostTerms,
            cancellationToken: cancellationToken);

        return keywordResults.Select((r, index) => new HybridSearchResult
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
    /// Performs hybrid search combining vector and keyword results with RRF fusion.
    /// </summary>
    private async Task<List<HybridSearchResult>> SearchHybridAsync(
        string query,
        Guid gameId,
        int limit,
        float vectorWeight,
        float keywordWeight,
        CancellationToken cancellationToken)
    {
        // Fetch more results from each system than needed (to ensure good fusion)
        // Retrieve 2x limit from each to have better coverage
        var fetchLimit = Math.Max(limit * 2, 20);

        // Generate query embedding first (needed for vector search)
        var embeddingResult = await _embeddingService.GenerateEmbeddingAsync(query, "en", cancellationToken);
        if (!embeddingResult.Success || embeddingResult.Embeddings.Count == 0)
        {
            _logger.LogError("Failed to generate query embedding for hybrid search: {Error}", embeddingResult.ErrorMessage);
            // Fall back to keyword-only search if embedding fails
            return await SearchKeywordOnlyAsync(query, gameId, limit, cancellationToken);
        }

        var queryEmbedding = embeddingResult.Embeddings[0];

        // Execute vector and keyword searches in parallel for performance
        var vectorTask = _qdrantService.SearchAsync(
            gameId.ToString(),
            queryEmbedding,
            limit: fetchLimit,
            ct: cancellationToken);

        var keywordTask = _keywordSearchService.SearchAsync(
            query,
            gameId,
            fetchLimit,
            phraseSearch: query.Contains("\""),
            boostTerms: _config.BoostTerms,
            cancellationToken: cancellationToken);

        await Task.WhenAll(vectorTask, keywordTask);

        var vectorResults = await vectorTask;
        var keywordResults = await keywordTask;

        if (!vectorResults.Success)
        {
            _logger.LogWarning("Vector search failed, using keyword-only results: {Error}", vectorResults.ErrorMessage);
            return await SearchKeywordOnlyAsync(query, gameId, limit, cancellationToken);
        }

        _logger.LogInformation(
            "Retrieved results for fusion: vectorCount={VectorCount}, keywordCount={KeywordCount}",
            vectorResults.Results.Count, keywordResults.Count);

        // Apply Reciprocal Rank Fusion (RRF) algorithm
        var fusedResults = FuseSearchResults(
            vectorResults.Results,
            keywordResults,
            gameId,
            vectorWeight,
            keywordWeight,
            _config.RrfConstant ?? DefaultRrfK);

        // Take top N results after fusion
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
        List<KeywordSearchResult> keywordResults,
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
            .ToDictionary(x => x.ChunkId);

        var keywordLookup = keywordResults
            .Select((r, index) => new { ChunkId = r.ChunkId, Result = r, Rank = index + 1 })
            .ToDictionary(x => x.ChunkId);

        // Collect all unique chunk IDs from both result sets
        var allChunkIds = vectorLookup.Keys
            .Union(keywordLookup.Keys)
            .ToHashSet();

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
public class HybridSearchConfiguration
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
    public List<string> BoostTerms { get; set; } = new();
}

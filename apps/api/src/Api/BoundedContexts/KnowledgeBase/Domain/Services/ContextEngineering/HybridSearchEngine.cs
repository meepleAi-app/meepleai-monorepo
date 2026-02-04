using Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;
using Api.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.ContextEngineering;

/// <summary>
/// Hybrid search engine integrating with Context Engineering Framework.
/// Issue #3492: Keyword + Vector + Reranking pipeline.
/// </summary>
/// <remarks>
/// 3-Stage Pipeline:
/// Stage 1: Parallel Retrieval (20 results each)
/// - Keyword Search (BM25) → Exact term matches
/// - Vector Search (embedding) → Semantic similarity
/// Stage 2: Reciprocal Rank Fusion (configurable weights)
/// Stage 3: Cross-Encoder Reranking (optional)
/// </remarks>
internal interface IHybridSearchEngine
{
    /// <summary>
    /// Executes hybrid search with optional reranking.
    /// </summary>
    Task<HybridSearchEngineResult> SearchAsync(
        HybridSearchEngineRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets current search configuration (for A/B testing).
    /// </summary>
    HybridSearchEngineConfig GetConfiguration();

    /// <summary>
    /// Gets A/B test variant metrics.
    /// </summary>
    HybridSearchMetrics GetMetrics();
}

/// <summary>
/// Hybrid search engine implementation with A/B testing support.
/// </summary>
internal sealed class HybridSearchEngine : IHybridSearchEngine
{
    private static readonly char[] WordSeparators = [' ', '\n', '\r', '\t'];

    private readonly IHybridSearchService _hybridSearchService;
    private readonly IRerankedRetrievalService? _rerankerService;
    private readonly ILogger<HybridSearchEngine> _logger;
    private readonly HybridSearchEngineConfig _config;
    private readonly HybridSearchMetrics _metrics;

    public HybridSearchEngine(
        IHybridSearchService hybridSearchService,
        IRerankedRetrievalService? rerankerService,
        ILogger<HybridSearchEngine> logger,
        HybridSearchEngineConfig? config = null)
    {
        _hybridSearchService = hybridSearchService ?? throw new ArgumentNullException(nameof(hybridSearchService));
        _rerankerService = rerankerService;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? new HybridSearchEngineConfig();
        _metrics = new HybridSearchMetrics();
    }

    public async Task<HybridSearchEngineResult> SearchAsync(
        HybridSearchEngineRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var metrics = new SearchExecutionMetrics();

        try
        {
            // Get A/B test variant weights
            var (vectorWeight, keywordWeight) = GetWeightsForVariant(request.AbTestVariant);

            // Stage 1 & 2: Parallel retrieval + RRF fusion
            var searchStopwatch = System.Diagnostics.Stopwatch.StartNew();

            var hybridResults = await _hybridSearchService.SearchAsync(
                request.Query,
                request.GameId,
                SearchMode.Hybrid,
                limit: request.MaxResults * 2, // Fetch 2x for reranking
                documentIds: request.DocumentIds,
                vectorWeight: vectorWeight,
                keywordWeight: keywordWeight,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            searchStopwatch.Stop();
            metrics.SearchDurationMs = searchStopwatch.ElapsedMilliseconds;

            _logger.LogDebug(
                "Hybrid search stage complete: {ResultCount} results in {DurationMs}ms",
                hybridResults.Count, metrics.SearchDurationMs);

            var items = ConvertToContextItems(hybridResults, request.Query);

            // Stage 3: Cross-encoder reranking (optional)
            if (request.EnableReranking && _rerankerService != null && items.Count > 0)
            {
                var rerankStopwatch = System.Diagnostics.Stopwatch.StartNew();

                try
                {
                    var rerankedItems = await RerankItemsAsync(
                        items,
                        request.Query,
                        request.GameId,
                        request.MaxResults,
                        cancellationToken).ConfigureAwait(false);

                    rerankStopwatch.Stop();
                    metrics.RerankDurationMs = rerankStopwatch.ElapsedMilliseconds;
                    metrics.RerankerUsed = true;

                    items = rerankedItems;

                    _logger.LogDebug(
                        "Reranking complete: {ResultCount} results in {DurationMs}ms",
                        items.Count, metrics.RerankDurationMs);
                }
                catch (Exception ex)
                {
                    rerankStopwatch.Stop();
                    _logger.LogWarning(ex, "Reranking failed, using RRF results");
                    metrics.RerankerUsed = false;
                    metrics.RerankerError = ex.Message;
                }
            }

            // Take top results
            var finalItems = items.Take(request.MaxResults).ToList();

            stopwatch.Stop();
            metrics.TotalDurationMs = stopwatch.ElapsedMilliseconds;
            metrics.ResultCount = finalItems.Count;

            // Record metrics for A/B testing
            RecordMetrics(request.AbTestVariant, metrics);

            return new HybridSearchEngineResult
            {
                Items = finalItems,
                Metrics = metrics,
                VectorWeight = vectorWeight,
                KeywordWeight = keywordWeight,
                AbTestVariant = request.AbTestVariant
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Hybrid search engine failed for query: {Query}", request.Query);

            return new HybridSearchEngineResult
            {
                Items = new List<RetrievedContextItem>(),
                Metrics = new SearchExecutionMetrics
                {
                    TotalDurationMs = stopwatch.ElapsedMilliseconds,
                    Error = ex.Message
                },
                VectorWeight = _config.DefaultVectorWeight,
                KeywordWeight = _config.DefaultKeywordWeight,
                AbTestVariant = request.AbTestVariant
            };
        }
    }

    public HybridSearchEngineConfig GetConfiguration() => _config;

    public HybridSearchMetrics GetMetrics() => _metrics;

    private (float vectorWeight, float keywordWeight) GetWeightsForVariant(string? variant)
    {
        if (string.IsNullOrEmpty(variant))
        {
            return (_config.DefaultVectorWeight, _config.DefaultKeywordWeight);
        }

        // A/B test variants
        return variant.ToUpperInvariant() switch
        {
            "CONTROL" => (_config.DefaultVectorWeight, _config.DefaultKeywordWeight),
            "SEMANTIC_HEAVY" => (0.8f, 0.2f),
            "BALANCED" => (0.5f, 0.5f),
            "KEYWORD_HEAVY" => (0.3f, 0.7f),
            _ => (_config.DefaultVectorWeight, _config.DefaultKeywordWeight)
        };
    }

    private List<RetrievedContextItem> ConvertToContextItems(
        List<HybridSearchResult> hybridResults,
        string query)
    {
        return hybridResults.Select(r => new RetrievedContextItem
        {
            Id = r.ChunkId,
            Content = r.Content,
            Relevance = r.HybridScore,
            TokenCount = EstimateTokens(r.Content),
            ContentType = "knowledge",
            Metadata = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["document_id"] = r.PdfDocumentId,
                ["game_id"] = r.GameId.ToString(),
                ["page_number"] = r.PageNumber ?? 0,
                ["chunk_index"] = r.ChunkIndex,
                ["vector_score"] = r.VectorScore?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
                ["keyword_score"] = r.KeywordScore?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
                ["vector_rank"] = r.VectorRank?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
                ["keyword_rank"] = r.KeywordRank?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
                ["matched_terms"] = string.Join(",", r.MatchedTerms)
            }
        }).ToList();
    }

    private async Task<List<RetrievedContextItem>> RerankItemsAsync(
        List<RetrievedContextItem> items,
        string query,
        Guid gameId,
        int topK,
        CancellationToken cancellationToken)
    {
        if (_rerankerService == null)
            return items;

        var request = new RerankedRetrievalRequest(
            Query: query,
            GameId: gameId,
            TopK: topK,
            ExpandToParent: false,
            Mode: RetrievalMode.Hybrid);

        var result = await _rerankerService.RetrieveAsync(request, cancellationToken).ConfigureAwait(false);

        // Check if reranker fell back (indicates potential issue)
        if (result.FallbackReason != null)
        {
            _logger.LogWarning("Reranker used fallback: {Reason}", result.FallbackReason);
            return items;
        }

        // Map reranked results back to context items
        return result.Results.Select(r => new RetrievedContextItem
        {
            Id = r.ChunkId,
            Content = r.Content,
            Relevance = r.EffectiveScore,
            TokenCount = EstimateTokens(r.Content),
            ContentType = "knowledge",
            Metadata = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["original_score"] = r.OriginalScore.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["rerank_score"] = r.RerankScore?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
                ["final_score"] = r.EffectiveScore.ToString(System.Globalization.CultureInfo.InvariantCulture)
            }
        }).ToList();
    }

    private static int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        // Simple estimation: ~1.3 tokens per word
        var wordCount = text.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries).Length;

        return (int)Math.Ceiling(wordCount * 1.3);
    }

    private void RecordMetrics(string? variant, SearchExecutionMetrics metrics)
    {
        var key = variant ?? "default";

        lock (_metrics)
        {
            if (!_metrics.VariantMetrics.TryGetValue(key, out var variantMetrics))
            {
                variantMetrics = new VariantMetrics();
                _metrics.VariantMetrics[key] = variantMetrics;
            }

            variantMetrics.TotalSearches++;
            variantMetrics.TotalSearchTimeMs += metrics.SearchDurationMs;
            variantMetrics.TotalRerankTimeMs += metrics.RerankDurationMs;
            variantMetrics.TotalResults += metrics.ResultCount;

            if (metrics.RerankerUsed)
                variantMetrics.RerankingUsedCount++;

            if (metrics.Error != null)
                variantMetrics.ErrorCount++;
        }
    }
}

/// <summary>
/// Request for hybrid search engine.
/// </summary>
public sealed record HybridSearchEngineRequest
{
    /// <summary>
    /// The search query.
    /// </summary>
    public required string Query { get; init; }

    /// <summary>
    /// Game ID for context filtering.
    /// </summary>
    public Guid GameId { get; init; }

    /// <summary>
    /// Maximum results to return.
    /// </summary>
    public int MaxResults { get; init; } = 10;

    /// <summary>
    /// Optional document ID filtering.
    /// </summary>
    public List<Guid>? DocumentIds { get; init; }

    /// <summary>
    /// Whether to apply cross-encoder reranking.
    /// </summary>
    public bool EnableReranking { get; init; } = true;

    /// <summary>
    /// A/B test variant identifier.
    /// </summary>
    public string? AbTestVariant { get; init; }
}

/// <summary>
/// Result from hybrid search engine.
/// </summary>
public sealed record HybridSearchEngineResult
{
    /// <summary>
    /// Retrieved context items.
    /// </summary>
    public required IReadOnlyList<RetrievedContextItem> Items { get; init; }

    /// <summary>
    /// Execution metrics.
    /// </summary>
    public required SearchExecutionMetrics Metrics { get; init; }

    /// <summary>
    /// Vector weight used.
    /// </summary>
    public float VectorWeight { get; init; }

    /// <summary>
    /// Keyword weight used.
    /// </summary>
    public float KeywordWeight { get; init; }

    /// <summary>
    /// A/B test variant used.
    /// </summary>
    public string? AbTestVariant { get; init; }
}

/// <summary>
/// Configuration for hybrid search engine.
/// </summary>
public sealed class HybridSearchEngineConfig
{
    /// <summary>
    /// Default weight for vector search (default: 0.6).
    /// </summary>
    public float DefaultVectorWeight { get; init; } = 0.6f;

    /// <summary>
    /// Default weight for keyword search (default: 0.4).
    /// </summary>
    public float DefaultKeywordWeight { get; init; } = 0.4f;

    /// <summary>
    /// Maximum parallel fetch limit (default: 20).
    /// </summary>
    public int MaxFetchLimit { get; init; } = 20;

    /// <summary>
    /// Reranking timeout in milliseconds (default: 5000).
    /// </summary>
    public int RerankTimeoutMs { get; init; } = 5000;

    /// <summary>
    /// Whether A/B testing is enabled.
    /// </summary>
    public bool AbTestingEnabled { get; init; }
}

/// <summary>
/// Metrics for a single search execution.
/// </summary>
public sealed class SearchExecutionMetrics
{
    /// <summary>
    /// Total execution time in milliseconds.
    /// </summary>
    public long TotalDurationMs { get; set; }

    /// <summary>
    /// Time spent in hybrid search (vector + keyword + RRF).
    /// </summary>
    public long SearchDurationMs { get; set; }

    /// <summary>
    /// Time spent in cross-encoder reranking.
    /// </summary>
    public long RerankDurationMs { get; set; }

    /// <summary>
    /// Number of results returned.
    /// </summary>
    public int ResultCount { get; set; }

    /// <summary>
    /// Whether reranker was successfully used.
    /// </summary>
    public bool RerankerUsed { get; set; }

    /// <summary>
    /// Error message if reranker failed.
    /// </summary>
    public string? RerankerError { get; set; }

    /// <summary>
    /// General error message if search failed.
    /// </summary>
    public string? Error { get; set; }
}

/// <summary>
/// Aggregate metrics for A/B testing.
/// </summary>
public sealed class HybridSearchMetrics
{
    /// <summary>
    /// Metrics by A/B test variant.
    /// </summary>
    public Dictionary<string, VariantMetrics> VariantMetrics { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Metrics for a specific A/B test variant.
/// </summary>
public sealed class VariantMetrics
{
    /// <summary>
    /// Total number of searches.
    /// </summary>
    public int TotalSearches { get; set; }

    /// <summary>
    /// Total search time in milliseconds.
    /// </summary>
    public long TotalSearchTimeMs { get; set; }

    /// <summary>
    /// Total reranking time in milliseconds.
    /// </summary>
    public long TotalRerankTimeMs { get; set; }

    /// <summary>
    /// Total results returned.
    /// </summary>
    public int TotalResults { get; set; }

    /// <summary>
    /// Number of searches where reranking was used.
    /// </summary>
    public int RerankingUsedCount { get; set; }

    /// <summary>
    /// Number of errors.
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Average search time in milliseconds.
    /// </summary>
    public double AverageSearchTimeMs => TotalSearches > 0 ? (double)TotalSearchTimeMs / TotalSearches : 0;

    /// <summary>
    /// Average rerank time in milliseconds.
    /// </summary>
    public double AverageRerankTimeMs => RerankingUsedCount > 0 ? (double)TotalRerankTimeMs / RerankingUsedCount : 0;

    /// <summary>
    /// Average results per search.
    /// </summary>
    public double AverageResults => TotalSearches > 0 ? (double)TotalResults / TotalSearches : 0;
}

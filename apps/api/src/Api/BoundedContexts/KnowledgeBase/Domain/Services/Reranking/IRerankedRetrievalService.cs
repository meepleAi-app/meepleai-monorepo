using Api.BoundedContexts.KnowledgeBase.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;

/// <summary>
/// ADR-016 Phase 4: Resilient retrieval service with cross-encoder reranking.
/// Orchestrates hybrid search → reranking → parent resolution pipeline.
/// Provides graceful degradation when reranker is unavailable.
/// </summary>
public interface IRerankedRetrievalService
{
    /// <summary>
    /// Performs reranked retrieval with parent chunk resolution.
    /// Falls back to RRF fusion when reranker is unavailable.
    /// </summary>
    /// <param name="request">Retrieval request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Retrieval results with reranking and expanded context.</returns>
    Task<RerankedRetrievalResult> RetrieveAsync(
        RerankedRetrievalRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current status of the reranking pipeline.
    /// </summary>
    /// <returns>Pipeline status information.</returns>
    Task<RerankedRetrievalStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Request for reranked retrieval.
/// </summary>
public sealed record RerankedRetrievalRequest(
    string Query,
    Guid GameId,
    int TopK = 10,
    bool ExpandToParent = true,
    RetrievalMode Mode = RetrievalMode.Hybrid
);

/// <summary>
/// Mode for initial retrieval.
/// </summary>
public enum RetrievalMode
{
    /// <summary>Vector-only search.</summary>
    Vector,

    /// <summary>Keyword-only search (BM25/FTS).</summary>
    Keyword,

    /// <summary>Hybrid search with RRF fusion (default).</summary>
    Hybrid
}

/// <summary>
/// Result of reranked retrieval.
/// </summary>
public sealed record RerankedRetrievalResult(
    IReadOnlyList<RerankedSearchResult> Results,
    RetrievalMetrics Metrics,
    bool UsedReranker,
    string? FallbackReason = null
);

/// <summary>
/// Individual search result with reranking information.
/// </summary>
public sealed record RerankedSearchResult(
    string ChunkId,
    string Content,
    double OriginalScore,
    double? RerankScore,
    int FinalRank,
    string? ParentChunkId = null,
    string? ParentContent = null,
    int? PageNumber = null,
    Dictionary<string, object>? Metadata = null
)
{
    /// <summary>
    /// Effective score for ranking (rerank if available, otherwise original).
    /// </summary>
    public double EffectiveScore => RerankScore ?? OriginalScore;
}

/// <summary>
/// Metrics for retrieval operation.
/// </summary>
public sealed record RetrievalMetrics(
    double TotalTimeMs,
    double SearchTimeMs,
    double? RerankTimeMs,
    double? ParentResolutionTimeMs,
    int CandidatesRetrieved,
    int ResultsReturned
);

/// <summary>
/// Status of the reranked retrieval pipeline.
/// </summary>
public sealed record RerankedRetrievalStatus(
    bool RerankerAvailable,
    string RerankerModel,
    DateTime LastHealthCheck,
    int ConsecutiveFailures
);

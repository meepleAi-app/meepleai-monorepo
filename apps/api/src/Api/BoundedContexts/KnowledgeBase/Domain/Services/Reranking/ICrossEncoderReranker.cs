

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;

/// <summary>
/// ADR-016 Phase 4: Cross-encoder reranking service interface.
/// Provides semantic reranking using BGE-reranker-v2-m3 model.
/// </summary>
public interface ICrossEncoderReranker
{
    /// <summary>
    /// Reranks chunks using cross-encoder semantic similarity.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="chunks">Chunks to rerank.</param>
    /// <param name="topK">Maximum number of results to return (null = all).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Reranked chunks sorted by semantic relevance.</returns>
    Task<RerankResult> RerankAsync(
        string query,
        IReadOnlyList<RerankChunk> chunks,
        int? topK = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the reranking service is healthy and available.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if service is available, false otherwise.</returns>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Input chunk for reranking.
/// </summary>
public sealed record RerankChunk(
    string Id,
    string Content,
    double OriginalScore,
    Dictionary<string, object>? Metadata = null
);

/// <summary>
/// Reranked chunk with cross-encoder score.
/// </summary>
public sealed record RerankedChunk(
    string Id,
    string Content,
    double OriginalScore,
    double RerankScore,
    Dictionary<string, object>? Metadata = null
);

/// <summary>
/// Result of reranking operation.
/// </summary>
public sealed record RerankResult(
    IReadOnlyList<RerankedChunk> Chunks,
    string Model,
    double ProcessingTimeMs
);

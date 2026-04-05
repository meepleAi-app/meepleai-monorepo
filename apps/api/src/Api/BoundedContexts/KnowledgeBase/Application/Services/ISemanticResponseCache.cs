namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

internal interface ISemanticResponseCache
{
    /// <summary>
    /// Try to get a cached response for a semantically similar query.
    /// Returns null on cache miss or if similarity &lt; threshold.
    /// </summary>
    Task<CachedRagResponse?> TryGetAsync(
        Guid gameId,
        float[] queryVector,
        CancellationToken ct = default);

    /// <summary>
    /// Store a RAG response in the semantic cache.
    /// </summary>
    Task SetAsync(
        Guid gameId,
        float[] queryVector,
        CachedRagResponse response,
        CancellationToken ct = default);

    /// <summary>
    /// Invalidate all cached responses for a game (called on re-index).
    /// </summary>
    Task InvalidateGameAsync(Guid gameId, CancellationToken ct = default);
}

internal sealed record CachedRagResponse(
    string Answer,
    IReadOnlyList<string> Citations,
    string ModelUsed,
    DateTimeOffset CachedAt);

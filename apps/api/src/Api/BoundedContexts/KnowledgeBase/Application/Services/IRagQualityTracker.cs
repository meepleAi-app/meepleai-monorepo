namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

internal interface IRagQualityTracker
{
    Task TrackQueryAsync(RagQueryMetrics metrics, CancellationToken ct = default);
}

internal sealed record RagQueryMetrics(
    Guid? ThreadId,
    Guid? GameId,
    int QueryLength,
    int ChunksRetrieved,
    int ChunksUsed,
    int CitationsCount,
    string Strategy,
    string ModelUsed,
    int LatencyMs,
    bool CacheHit,
    bool NoRelevantContext,
    int? InputTokens = null,
    int? OutputTokens = null,
    decimal? ContextPrecision = null);

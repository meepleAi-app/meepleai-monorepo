using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

internal sealed class RagQualityTracker(
    MeepleAiDbContext db,
    ILogger<RagQualityTracker> logger) : IRagQualityTracker
{
    public async Task TrackQueryAsync(RagQueryMetrics metrics, CancellationToken ct = default)
    {
        try
        {
            var entity = new RagQualityLogEntity
            {
                Id = Guid.NewGuid(),
                ThreadId = metrics.ThreadId,
                GameId = metrics.GameId,
                QueryLength = metrics.QueryLength,
                ChunksRetrieved = metrics.ChunksRetrieved,
                ChunksUsed = metrics.ChunksUsed,
                ContextPrecision = metrics.ContextPrecision,
                CitationsCount = metrics.CitationsCount,
                Strategy = metrics.Strategy,
                ModelUsed = metrics.ModelUsed,
                InputTokens = metrics.InputTokens,
                OutputTokens = metrics.OutputTokens,
                LatencyMs = metrics.LatencyMs,
                CacheHit = metrics.CacheHit,
                NoRelevantContext = metrics.NoRelevantContext,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.RagQualityLogs.Add(entity);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Non-critical: log and continue — never fail a RAG response for metrics
            logger.LogWarning(ex, "Failed to save RAG quality metrics — non-critical");
        }
    }
}

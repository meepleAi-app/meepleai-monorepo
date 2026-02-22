using Api.Services;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// Repository for LLM request log persistence.
/// Issue #5072: detailed request tracking with 30-day retention.
/// </summary>
public interface ILlmRequestLogRepository
{
    /// <summary>
    /// Persists a new LLM request log entry.
    /// ExpiresAt is automatically set to RequestedAt + 30 days.
    /// </summary>
    Task LogRequestAsync(
        string modelId,
        string provider,
        RequestSource source,
        Guid? userId,
        string? userRole,
        int promptTokens,
        int completionTokens,
        decimal costUsd,
        int latencyMs,
        bool success,
        string? errorMessage,
        bool isStreaming,
        bool isFreeModel,
        string? sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the total number of LLM request log entries recorded on the specified UTC date.
    /// Used by the admin usage dashboard to display today's request count.
    /// </summary>
    Task<int> GetTodayCountAsync(DateOnly utcDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all log entries with ExpiresAt older than the specified cutoff.
    /// Returns number of deleted records.
    /// </summary>
    Task<int> DeleteExpiredAsync(DateTime cutoff, CancellationToken cancellationToken = default);
}

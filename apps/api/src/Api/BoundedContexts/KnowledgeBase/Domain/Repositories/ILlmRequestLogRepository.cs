using Api.Infrastructure.Entities;
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
        string? userRegion = null,
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

    /// <summary>
    /// Issue #5509: Deletes all LLM request log entries for a specific user (GDPR Art. 17 right to erasure).
    /// Returns the number of deleted records.
    /// </summary>
    Task<int> DeleteByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns per-source request counts grouped into time buckets (hourly or daily).
    /// Issue #5078: request timeline chart data.
    /// </summary>
    Task<IReadOnlyList<(DateTime Bucket, string Source, int Count, decimal CostUsd)>> GetTimelineAsync(
        DateTime from,
        DateTime until,
        bool groupByHour,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns aggregated cost data grouped by model, source, and user tier/role.
    /// Issue #5080: cost breakdown panel data.
    /// </summary>
    Task<(
        IReadOnlyList<(string ModelId, decimal CostUsd, int Requests, int TotalTokens)> ByModel,
        IReadOnlyList<(string Source, decimal CostUsd, int Requests)> BySource,
        IReadOnlyList<(string Tier, decimal CostUsd, int Requests)> ByTier,
        decimal TotalCostUsd,
        int TotalRequests
    )> GetCostBreakdownAsync(
        DateTime from,
        DateTime until,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns today's request counts grouped by free model (IsFreeModel = true).
    /// Issue #5082: free quota indicator data.
    /// </summary>
    Task<IReadOnlyList<(string ModelId, int RequestsToday)>> GetFreeModelUsageAsync(
        DateOnly forDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Issue #5511: Pseudonymizes UserId with salted SHA-256 hash for logs older than the cutoff.
    /// Only processes records where IsAnonymized = false and UserId is not null.
    /// Returns the number of pseudonymized records.
    /// </summary>
    Task<int> PseudonymizeOldLogsAsync(DateTime cutoff, string salt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a paginated, optionally filtered list of recent LLM request log entries.
    /// Issue #5083: recent requests table data.
    /// </summary>
    Task<(IReadOnlyList<LlmRequestLogEntity> Items, int Total)> GetPagedAsync(
        string? source,
        string? model,
        DateTime? from,
        DateTime? until,
        bool? successOnly,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}

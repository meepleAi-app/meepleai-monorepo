using Api.BoundedContexts.KnowledgeBase.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// Repository for RAG execution history persistence and querying.
/// Issue #4458: RAG Execution History
/// </summary>
public interface IRagExecutionRepository
{
    Task AddAsync(RagExecution execution, CancellationToken cancellationToken = default);

    Task<RagExecution?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(List<RagExecution> Items, int TotalCount)> GetPagedAsync(
        int skip,
        int take,
        string? strategy = null,
        string? status = null,
        int? minLatencyMs = null,
        int? maxLatencyMs = null,
        double? minConfidence = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        CancellationToken cancellationToken = default);

    Task<RagExecutionStats> GetStatsAsync(
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        CancellationToken cancellationToken = default);

    Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken = default);
}

/// <summary>
/// Aggregated stats for RAG executions.
/// </summary>
public record RagExecutionStats(
    int TotalExecutions,
    double AvgLatencyMs,
    double ErrorRate,
    double CacheHitRate,
    decimal TotalCost,
    double AvgConfidence);

using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;

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

    /// <summary>
    /// Get execution stats grouped by AgentDefinitionId for the agent catalog.
    /// Issue #3713: Agent Catalog and Usage Stats
    /// </summary>
    Task<List<AgentExecutionStats>> GetStatsByAgentAsync(
        DateTime dateFrom,
        DateTime dateTo,
        Guid? agentDefinitionId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get time-series data for agent executions.
    /// Issue #3713: Agent Catalog and Usage Stats
    /// </summary>
    Task<List<AgentTimeSeriesPoint>> GetTimeSeriesByAgentAsync(
        Guid agentDefinitionId,
        DateTime dateFrom,
        DateTime dateTo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get time-series data for multiple agents in a single query.
    /// Issue #3713: Agent Catalog and Usage Stats - Batch operation to avoid N+1.
    /// </summary>
    Task<Dictionary<Guid, List<AgentTimeSeriesPoint>>> GetTimeSeriesByAgentsAsync(
        List<Guid> agentDefinitionIds,
        DateTime dateFrom,
        DateTime dateTo,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StrategyAggregateMetrics>> GetAggregatedMetricsAsync(
        DateOnly? startDate, DateOnly? endDate,
        string? strategy = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimeSeriesPoint>> GetTimeSeriesMetricsAsync(
        string strategy,
        DateOnly startDate, DateOnly endDate,
        TimeSeriesGranularity granularity,
        CancellationToken cancellationToken = default);
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

/// <summary>
/// Per-agent execution stats aggregation.
/// Issue #3713: Agent Catalog and Usage Stats
/// </summary>
public record AgentExecutionStats(
    Guid AgentDefinitionId,
    string? AgentName,
    string? Model,
    string? Provider,
    int ExecutionCount,
    int TotalTokens,
    double AvgTokens,
    decimal TotalCost,
    double SuccessRate,
    double AvgLatencyMs,
    double AvgConfidence,
    DateTime? LastExecutedAt);

/// <summary>
/// Time-series data point for agent execution charts.
/// Issue #3713: Agent Catalog and Usage Stats
/// </summary>
public record AgentTimeSeriesPoint(
    DateTime Date,
    int Executions,
    int TotalTokens,
    decimal Cost,
    double AvgLatencyMs,
    double SuccessRate);

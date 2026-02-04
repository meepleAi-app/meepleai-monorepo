using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

// Issue #3304: RAG Dashboard queries for configuration and metrics.

#region Configuration Queries

/// <summary>
/// Query to get RAG configuration for a user or global defaults.
/// </summary>
public sealed record GetRagConfigQuery : IQuery<RagConfigDto>
{
    /// <summary>
    /// User ID for user-specific config, null for global defaults.
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// Strategy to get config for, or null for active strategy.
    /// </summary>
    public string? Strategy { get; init; }
}

/// <summary>
/// Query to get all available RAG dashboard options.
/// </summary>
public sealed record GetRagDashboardOptionsQuery : IQuery<RagDashboardOptionsDto>;

#endregion

#region Metrics Queries

/// <summary>
/// Query to get RAG dashboard overview with all strategy metrics.
/// </summary>
public sealed record GetRagDashboardOverviewQuery : IQuery<RagDashboardOverviewDto>
{
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
}

/// <summary>
/// Query to get metrics for a specific strategy.
/// </summary>
public sealed record GetStrategyMetricsQuery : IQuery<StrategyMetricsDto>
{
    public required string StrategyId { get; init; }
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
}

/// <summary>
/// Query to get time series metrics for a strategy.
/// </summary>
public sealed record GetStrategyTimeSeriesQuery : IQuery<StrategyTimeSeriesMetricsDto>
{
    public required string StrategyId { get; init; }
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public string Granularity { get; init; } = "hour"; // hour, day, week
}

/// <summary>
/// Query to compare multiple strategies.
/// </summary>
public sealed record GetStrategyComparisonQuery : IQuery<StrategyComparisonDto>
{
    public IReadOnlyList<string>? StrategyIds { get; init; } // null = all strategies
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
}

#endregion

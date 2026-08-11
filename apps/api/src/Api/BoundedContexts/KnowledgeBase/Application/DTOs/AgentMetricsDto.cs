namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// DTO for aggregated agent metrics.
/// Issue #3382: Agent Metrics Dashboard.
/// </summary>
internal record AgentMetricsDto(
    int TotalInvocations,
    int TotalTokensUsed,
    decimal TotalCost,
    double AvgLatencyMs,
    double AvgConfidenceScore,
    double UserSatisfactionRate,
    IReadOnlyList<TopQueryDto> TopQueries,
    IReadOnlyList<MetricsCostBreakdownDto> CostBreakdown,
    IReadOnlyList<UsageOverTimeDto> UsageOverTime
);

/// <summary>
/// DTO for top queries by frequency.
/// </summary>
internal record TopQueryDto(
    string Query,
    int Count
);

/// <summary>
/// DTO for cost breakdown by strategy/model for metrics dashboard.
/// Issue #3382: Agent Metrics Dashboard.
/// </summary>
internal record MetricsCostBreakdownDto(
    string Category,
    decimal Cost,
    int Invocations,
    int Tokens
);

/// <summary>
/// DTO for usage over time series.
/// </summary>
internal record UsageOverTimeDto(
    string Date,
    int Count,
    decimal Cost,
    int Tokens
);

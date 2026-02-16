namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// DTO for model performance analytics response.
/// Issue #3716: Model comparison dashboard with latency, cost, quality metrics.
/// </summary>
public record ModelPerformanceDto
{
    public required int TotalRequests { get; init; }
    public required decimal TotalCost { get; init; }
    public required int TotalTokens { get; init; }
    public required double AvgLatencyMs { get; init; }
    public required double SuccessRate { get; init; }
    public required List<ModelMetricsDto> Models { get; init; }
    public required List<DailyModelStats> DailyStats { get; init; }
}

/// <summary>
/// Per-model aggregated metrics for comparison.
/// </summary>
public record ModelMetricsDto
{
    public required string ModelId { get; init; }
    public required string Provider { get; init; }
    public required int RequestCount { get; init; }
    public required double UsagePercent { get; init; }
    public required decimal TotalCost { get; init; }
    public required double AvgLatencyMs { get; init; }
    public required int TotalTokens { get; init; }
    public required double SuccessRate { get; init; }
    public required double AvgTokensPerRequest { get; init; }
}

/// <summary>
/// Daily aggregated model stats for time series.
/// </summary>
public record DailyModelStats
{
    public required DateOnly Date { get; init; }
    public required int RequestCount { get; init; }
    public required decimal TotalCost { get; init; }
    public required double AvgLatencyMs { get; init; }
}

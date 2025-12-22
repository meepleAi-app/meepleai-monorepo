

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.Analytics;

/// <summary>
/// ISSUE-1725: Analyzes correlation between cache effectiveness and LLM cost savings.
/// Provides insights on cache ROI and optimization opportunities.
/// </summary>
internal interface ICacheCorrelationAnalyzer
{
    /// <summary>
    /// Analyze cache effectiveness and cost impact
    /// </summary>
    Task<CacheCorrelationReport> AnalyzeCacheEffectivenessAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculate estimated cost savings from cache hits
    /// </summary>
    Task<decimal> CalculateCacheSavingsAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Cache effectiveness correlation report
/// </summary>
internal record CacheCorrelationReport
{
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public required int TotalRequests { get; init; }
    public required int CacheHits { get; init; }
    public required int CacheMisses { get; init; }
    public required double HitRate { get; init; }
    public required decimal EstimatedSavingsUsd { get; init; }
    public required decimal CostWithoutCache { get; init; }
    public required decimal ActualCost { get; init; }
    public required double CacheEfficiencyScore { get; init; }
    public required List<string> Recommendations { get; init; }
}


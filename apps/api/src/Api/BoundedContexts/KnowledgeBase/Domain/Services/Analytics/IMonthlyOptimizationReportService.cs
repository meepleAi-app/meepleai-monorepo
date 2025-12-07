namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.Analytics;

/// <summary>
/// ISSUE-1725: Generates comprehensive monthly LLM optimization reports.
/// Combines efficiency analysis, model recommendations, and cache correlation.
/// </summary>
public interface IMonthlyOptimizationReportService
{
    /// <summary>
    /// Generate complete monthly optimization report
    /// </summary>
    Task<MonthlyOptimizationReport> GenerateReportAsync(
        int year,
        int month,
        CancellationToken ct = default);
}

/// <summary>
/// Comprehensive monthly optimization report
/// </summary>
public record MonthlyOptimizationReport
{
    public required int Year { get; init; }
    public required int Month { get; init; }
    public required QueryEfficiencyReport EfficiencyAnalysis { get; init; }
    public required CacheCorrelationReport CacheAnalysis { get; init; }
    public required List<ModelComparison> ModelComparisons { get; init; }
    public required ModelRecommendation RecommendedModel { get; init; }
    public required List<string> ExecutiveSummary { get; init; }
    public required decimal TotalSavingsOpportunity { get; init; }
}

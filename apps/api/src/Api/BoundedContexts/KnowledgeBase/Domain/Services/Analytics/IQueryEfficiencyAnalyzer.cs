

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.Analytics;

/// <summary>
/// ISSUE-1725: Analyzes LLM query efficiency by token usage patterns.
/// Identifies inefficient queries and provides optimization recommendations.
/// </summary>
public interface IQueryEfficiencyAnalyzer
{
    /// <summary>
    /// Analyze query efficiency metrics for a date range
    /// </summary>
    /// <param name="startDate">Start date (inclusive)</param>
    /// <param name="endDate">End date (inclusive)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Efficiency analysis report</returns>
    Task<QueryEfficiencyReport> AnalyzeEfficiencyAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default);

    /// <summary>
    /// Identify top N most expensive query types by total cost
    /// </summary>
    Task<IReadOnlyList<QueryTypeCost>> GetTopCostlyQueriesAsync(
        DateOnly startDate,
        DateOnly endDate,
        int topN = 10,
        CancellationToken ct = default);

    /// <summary>
    /// Calculate average tokens per query by operation type
    /// </summary>
    Task<IReadOnlyDictionary<string, double>> GetAverageTokensByOperationAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default);
}

/// <summary>
/// Query efficiency analysis report
/// </summary>
public record QueryEfficiencyReport
{
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public required int TotalQueries { get; init; }
    public required decimal TotalCost { get; init; }
    public required int TotalTokens { get; init; }
    public required double AverageTokensPerQuery { get; init; }
    public required decimal AverageCostPerQuery { get; init; }
    public required IReadOnlyList<QueryTypeCost> TopCostlyQueries { get; init; }
    public required IReadOnlyDictionary<string, double> AverageTokensByOperation { get; init; }
    public required IReadOnlyList<string> OptimizationRecommendations { get; init; }
}

/// <summary>
/// Cost breakdown by query type
/// </summary>
public record QueryTypeCost
{
    public required string QueryType { get; init; }
    public required int QueryCount { get; init; }
    public required decimal TotalCost { get; init; }
    public required int TotalTokens { get; init; }
    public required double AverageTokens { get; init; }
    public required decimal AverageCost { get; init; }
}
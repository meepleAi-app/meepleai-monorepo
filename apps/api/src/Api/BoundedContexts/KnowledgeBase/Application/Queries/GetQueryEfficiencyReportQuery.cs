using Api.BoundedContexts.KnowledgeBase.Domain.Services.Analytics;
using MediatR;

#pragma warning disable MA0048 // File name must match type name - Contains Query with Result record
namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// ISSUE-1725: Query for LLM query efficiency analysis report.
/// Returns token usage patterns and optimization recommendations.
/// </summary>
public record GetQueryEfficiencyReportQuery : IRequest<QueryEfficiencyReportDto>
{
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
}

/// <summary>
/// DTO for query efficiency report
/// </summary>
public record QueryEfficiencyReportDto
{
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public required int TotalQueries { get; init; }
    public required decimal TotalCost { get; init; }
    public required int TotalTokens { get; init; }
    public required double AverageTokensPerQuery { get; init; }
    public required decimal AverageCostPerQuery { get; init; }
    public required IReadOnlyList<QueryTypeCostDto> TopCostlyQueries { get; init; }
    public required IReadOnlyDictionary<string, double> AverageTokensByOperation { get; init; }
    public required IReadOnlyList<string> OptimizationRecommendations { get; init; }
}

public record QueryTypeCostDto
{
    public required string QueryType { get; init; }
    public required int QueryCount { get; init; }
    public required decimal TotalCost { get; init; }
    public required int TotalTokens { get; init; }
    public required double AverageTokens { get; init; }
    public required decimal AverageCost { get; init; }
}
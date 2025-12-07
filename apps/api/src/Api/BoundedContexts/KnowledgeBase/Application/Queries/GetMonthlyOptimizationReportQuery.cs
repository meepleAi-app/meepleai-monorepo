using Api.BoundedContexts.KnowledgeBase.Domain.Services.Analytics;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// ISSUE-1725: Query for monthly LLM optimization report.
/// Combines efficiency, cache, and model analysis.
/// </summary>
public record GetMonthlyOptimizationReportQuery : IRequest<MonthlyOptimizationReport>
{
    public required int Year { get; init; }
    public required int Month { get; init; }
}

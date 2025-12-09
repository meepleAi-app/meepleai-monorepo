using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Analytics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// ISSUE-1725: Handler for GetQueryEfficiencyReportQuery.
/// Analyzes LLM query efficiency and provides optimization recommendations.
/// </summary>
public class GetQueryEfficiencyReportQueryHandler
    : IRequestHandler<GetQueryEfficiencyReportQuery, QueryEfficiencyReportDto>
{
    private readonly IQueryEfficiencyAnalyzer _efficiencyAnalyzer;
    private readonly ILogger<GetQueryEfficiencyReportQueryHandler> _logger;

    public GetQueryEfficiencyReportQueryHandler(
        IQueryEfficiencyAnalyzer efficiencyAnalyzer,
        ILogger<GetQueryEfficiencyReportQueryHandler> logger)
    {
        _efficiencyAnalyzer = efficiencyAnalyzer ?? throw new ArgumentNullException(nameof(efficiencyAnalyzer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<QueryEfficiencyReportDto> Handle(
        GetQueryEfficiencyReportQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Generating query efficiency report from {StartDate} to {EndDate}",
            request.StartDate, request.EndDate);

        var report = await _efficiencyAnalyzer.AnalyzeEfficiencyAsync(
            request.StartDate,
            request.EndDate,
            cancellationToken).ConfigureAwait(false);

        // Map domain model to DTO
        var dto = new QueryEfficiencyReportDto
        {
            StartDate = report.StartDate,
            EndDate = report.EndDate,
            TotalQueries = report.TotalQueries,
            TotalCost = report.TotalCost,
            TotalTokens = report.TotalTokens,
            AverageTokensPerQuery = report.AverageTokensPerQuery,
            AverageCostPerQuery = report.AverageCostPerQuery,
            TopCostlyQueries = report.TopCostlyQueries.Select(q => new QueryTypeCostDto
            {
                QueryType = q.QueryType,
                QueryCount = q.QueryCount,
                TotalCost = q.TotalCost,
                TotalTokens = q.TotalTokens,
                AverageTokens = q.AverageTokens,
                AverageCost = q.AverageCost
            }).ToList(),
            AverageTokensByOperation = report.AverageTokensByOperation,
            OptimizationRecommendations = report.OptimizationRecommendations
        };

        _logger.LogInformation(
            "Query efficiency report generated: {TotalQueries} queries, ${TotalCost:F6} cost, {Recommendations} recommendations",
            dto.TotalQueries, dto.TotalCost, dto.OptimizationRecommendations.Count);

        return dto;
    }
}

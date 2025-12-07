using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Analytics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// ISSUE-1725: Handler for GetMonthlyOptimizationReportQuery.
/// Generates comprehensive monthly LLM optimization reports.
/// </summary>
public class GetMonthlyOptimizationReportQueryHandler
    : IRequestHandler<GetMonthlyOptimizationReportQuery, MonthlyOptimizationReport>
{
    private readonly IMonthlyOptimizationReportService _reportService;
    private readonly ILogger<GetMonthlyOptimizationReportQueryHandler> _logger;

    public GetMonthlyOptimizationReportQueryHandler(
        IMonthlyOptimizationReportService reportService,
        ILogger<GetMonthlyOptimizationReportQueryHandler> logger)
    {
        _reportService = reportService ?? throw new ArgumentNullException(nameof(reportService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<MonthlyOptimizationReport> Handle(
        GetMonthlyOptimizationReportQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Generating monthly optimization report for {Year}-{Month:D2}",
            request.Year, request.Month);

        var report = await _reportService.GenerateReportAsync(
            request.Year,
            request.Month,
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Monthly report generated: ${TotalCost:F2} spend, ${Savings:F2} optimization opportunity",
            report.EfficiencyAnalysis.TotalCost,
            report.TotalSavingsOpportunity);

        return report;
    }
}

using Api.Infrastructure;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Queries.QualityReports;

/// <summary>
/// Handles quality report generation for a specified date range.
/// Business logic: Date validation, aggregated metrics calculation (averages, counts, percentages).
/// Infrastructure delegation: Database query via DbContext.
/// Metrics: RAG confidence, LLM confidence, citation quality, overall confidence, low-quality percentage.
/// </summary>
internal sealed class GenerateQualityReportQueryHandler : IQueryHandler<GenerateQualityReportQuery, QualityReport>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GenerateQualityReportQueryHandler> _logger;

    public GenerateQualityReportQueryHandler(
        MeepleAiDbContext dbContext,
        ILogger<GenerateQualityReportQueryHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<QualityReport> Handle(GenerateQualityReportQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        // Business logic validation
        if (query.EndDate < query.StartDate)
        {
            throw new ArgumentException("End date must be greater than or equal to start date", nameof(query));
        }

        _logger.LogInformation(
            "Generating quality report for date range {StartDate} to {EndDate} ({Days} days)",
            query.StartDate, query.EndDate, query.Days);

        // Query AI request logs within date range
        var logs = await _dbContext.AiRequestLogs
            .Where(l => l.CreatedAt >= query.StartDate && l.CreatedAt <= query.EndDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var totalResponses = logs.Count;
        var lowQualityCount = logs.Count(l => l.IsLowQuality);

        // Business logic: Calculate aggregated metrics
        var report = new QualityReport
        {
            StartDate = query.StartDate,
            EndDate = query.EndDate,
            TotalResponses = totalResponses,
            LowQualityCount = lowQualityCount,
            LowQualityPercentage = totalResponses > 0 ? (lowQualityCount / (double)totalResponses) * 100 : 0.0,
            AverageRagConfidence = logs.Count > 0 ? logs.Average(l => l.RagConfidence) : null,
            AverageLlmConfidence = logs.Count > 0 ? logs.Average(l => l.LlmConfidence) : null,
            AverageCitationQuality = logs.Count > 0 ? logs.Average(l => l.CitationQuality) : null,
            AverageOverallConfidence = logs.Count > 0 ? logs.Average(l => l.OverallConfidence) : null
        };

        _logger.LogInformation(
            "Quality report generated: {TotalResponses} total, {LowQualityCount} low-quality ({LowQualityPercentage:F2}%), " +
            "Avg Overall Confidence: {AvgOverall:F3}",
            report.TotalResponses,
            report.LowQualityCount,
            report.LowQualityPercentage,
            report.AverageOverallConfidence ?? 0.0);

        return report;
    }
}

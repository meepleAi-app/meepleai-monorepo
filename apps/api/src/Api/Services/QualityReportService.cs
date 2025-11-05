using Api.Infrastructure;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
/// Interface for quality reporting service.
/// Provides manual report generation capability.
/// </summary>
public interface IQualityReportService
{
    /// <summary>
    /// Generate a quality report for the specified date range.
    /// </summary>
    /// <param name="startDate">Start date (inclusive)</param>
    /// <param name="endDate">End date (inclusive)</param>
    /// <returns>Quality report with aggregated statistics</returns>
    Task<QualityReport> GenerateReportAsync(DateTime startDate, DateTime endDate);
}

/// <summary>
/// Background service that periodically generates quality reports for AI responses.
/// Also provides manual report generation via the IQualityReportService interface.
/// AI-11: Monitors RAG confidence, LLM confidence, citation quality, and overall confidence metrics.
/// </summary>
public class QualityReportService : BackgroundService, IQualityReportService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QualityReportService> _logger;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _initialDelay;
    private readonly int _reportWindowDays;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Creates a new instance of the quality report service.
    /// </summary>
    /// <param name="scopeFactory">Service scope factory for creating scoped dependencies</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="configuration">Application configuration</param>
    public QualityReportService(
        IServiceScopeFactory scopeFactory,
        ILogger<QualityReportService> logger,
        IConfiguration configuration,
        TimeProvider? timeProvider = null)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;

        // Read configuration with defaults
        var intervalMinutes = configuration.GetValue<double>("QualityReporting:IntervalMinutes", 60);
        var initialDelayMinutes = configuration.GetValue<double>("QualityReporting:InitialDelayMinutes", 1);
        _reportWindowDays = configuration.GetValue<int>("QualityReporting:ReportWindowDays", 7);

        _interval = TimeSpan.FromMinutes(intervalMinutes);
        _initialDelay = TimeSpan.FromMinutes(initialDelayMinutes);

        // Validate configuration
        if (_interval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                $"QualityReporting:IntervalMinutes must be positive (got {intervalMinutes})");
        }

        if (_reportWindowDays <= 0)
        {
            throw new InvalidOperationException(
                $"QualityReporting:ReportWindowDays must be positive (got {_reportWindowDays})");
        }
    }

    /// <summary>
    /// Background service execution loop.
    /// Runs periodically to generate quality reports.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Quality report service started. Will run every {IntervalMinutes} minutes, report window: {WindowDays} days",
            _interval.TotalMinutes,
            _reportWindowDays);

        // Wait before first run to allow application to fully start
        await Task.Delay(_initialDelay, _timeProvider, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await GenerateScheduledReportAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Quality report generation cancelled (application shutting down)");
                break;
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // Justification: Background service boundary - prevents service crash
            // Background service: Generic catch prevents service from crashing host process
            // Report generation failure logged but service continues scheduled execution
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating scheduled quality report");
                // Continue to next iteration despite error
            }
#pragma warning restore CA1031

            // Wait for the configured interval before next run
            await Task.Delay(_interval, _timeProvider, stoppingToken);
        }

        _logger.LogInformation("Quality report service stopped");
    }

    /// <summary>
    /// Generate a quality report for the specified date range (public API).
    /// </summary>
    public async Task<QualityReport> GenerateReportAsync(DateTime startDate, DateTime endDate)
    {
        if (endDate < startDate)
        {
            throw new ArgumentException("End date must be greater than or equal to start date", nameof(endDate));
        }

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        return await GenerateReportInternalAsync(dbContext, startDate, endDate);
    }

    /// <summary>
    /// Generate scheduled report using configured window.
    /// </summary>
    private async Task GenerateScheduledReportAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running scheduled quality report generation");

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // Calculate date range (last N days)
        var endDate = _timeProvider.GetUtcNow().DateTime;
        var startDate = endDate.AddDays(-_reportWindowDays);

        var report = await GenerateReportInternalAsync(dbContext, startDate, endDate, cancellationToken);

        // Log summary
        _logger.LogInformation(
            "Quality report generated: {TotalResponses} total, {LowQualityCount} low-quality ({LowQualityPercentage:F2}%), " +
            "Avg Overall Confidence: {AvgOverall:F3}",
            report.TotalResponses,
            report.LowQualityCount,
            report.LowQualityPercentage,
            report.AverageOverallConfidence ?? 0.0);
    }

    /// <summary>
    /// Internal method to generate report from database context.
    /// </summary>
    private async Task<QualityReport> GenerateReportInternalAsync(
        MeepleAiDbContext dbContext,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        // Query AI request logs within date range
        var logs = await dbContext.AiRequestLogs
            .Where(l => l.CreatedAt >= startDate && l.CreatedAt <= endDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var totalResponses = logs.Count;
        var lowQualityCount = logs.Count(l => l.IsLowQuality);

        // Calculate aggregated metrics
        var report = new QualityReport
        {
            StartDate = startDate,
            EndDate = endDate,
            TotalResponses = totalResponses,
            LowQualityCount = lowQualityCount,
            LowQualityPercentage = totalResponses > 0 ? (lowQualityCount / (double)totalResponses) * 100 : 0.0,
            AverageRagConfidence = logs.Any() ? logs.Average(l => l.RagConfidence) : null,
            AverageLlmConfidence = logs.Any() ? logs.Average(l => l.LlmConfidence) : null,
            AverageCitationQuality = logs.Any() ? logs.Average(l => l.CitationQuality) : null,
            AverageOverallConfidence = logs.Any() ? logs.Average(l => l.OverallConfidence) : null
        };

        return report;
    }
}

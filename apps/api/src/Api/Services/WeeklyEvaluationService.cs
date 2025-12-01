using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries.QualityReports;
using Api.Models;
using MediatR;
using Microsoft.Extensions.Options;

namespace Api.Services;

/// <summary>
/// Configuration for weekly automated quality evaluation.
/// BGAI-042: Weekly automated quality evaluation job.
/// </summary>
public class WeeklyEvaluationConfiguration
{
    /// <summary>Evaluation interval in days (default: 7 days = weekly)</summary>
    public int IntervalDays { get; set; } = 7;

    /// <summary>Initial delay before first evaluation in minutes (default: 5 minutes)</summary>
    public double InitialDelayMinutes { get; set; } = 5;

    /// <summary>Report window in days (default: 7 days)</summary>
    public int ReportWindowDays { get; set; } = 7;

    /// <summary>Enable/disable the weekly evaluation service (default: true)</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Path to RAG evaluation dataset (optional)</summary>
    public string? RagDatasetPath { get; set; }

    /// <summary>Enable RAG evaluation (default: false, requires dataset)</summary>
    public bool EnableRagEvaluation { get; set; } = false;

    /// <summary>Quality thresholds for alerts</summary>
    public QualityThresholds Thresholds { get; set; } = new();
}

/// <summary>
/// Quality thresholds for triggering alerts.
/// </summary>
public class QualityThresholds
{
    /// <summary>Maximum acceptable low-quality percentage (default: 10%)</summary>
    public double MaxLowQualityPercentage { get; set; } = 10.0;

    /// <summary>Minimum acceptable overall confidence (default: 0.70)</summary>
    public double MinOverallConfidence { get; set; } = 0.70;

    /// <summary>Minimum acceptable RAG confidence (default: 0.70)</summary>
    public double MinRagConfidence { get; set; } = 0.70;
}

/// <summary>
/// Background service that runs weekly automated quality evaluation.
/// BGAI-042: Implements scheduled comprehensive quality assessment.
///
/// Features:
/// - Weekly execution (configurable interval)
/// - Quality report generation for past week
/// - Optional RAG performance evaluation
/// - Alert generation on quality degradation
/// - Comprehensive logging and monitoring
/// </summary>
public class WeeklyEvaluationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WeeklyEvaluationService> _logger;
    private readonly WeeklyEvaluationConfiguration _config;
    private readonly TimeProvider _timeProvider;

    public WeeklyEvaluationService(
        IServiceScopeFactory scopeFactory,
        ILogger<WeeklyEvaluationService> logger,
        IOptions<WeeklyEvaluationConfiguration> config,
        TimeProvider? timeProvider = null)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Check if service is enabled
        if (!_config.Enabled)
        {
            _logger.LogInformation(
                "Weekly evaluation service is disabled via configuration. Set QualityEvaluation:Enabled=true to enable.");
            return;
        }

        // Validate configuration
        if (_config.IntervalDays <= 0)
        {
            _logger.LogWarning(
                "Weekly evaluation interval is {Days} days (invalid). Service is disabled.",
                _config.IntervalDays);
            return;
        }

        if (_config.ReportWindowDays <= 0)
        {
            _logger.LogWarning(
                "Weekly evaluation report window is {Days} days (invalid). Service is disabled.",
                _config.ReportWindowDays);
            return;
        }

        _logger.LogInformation(
            "Weekly evaluation service started. Will run every {Days} days, report window: {WindowDays} days",
            _config.IntervalDays,
            _config.ReportWindowDays);

        // Wait before first run to allow application to fully start
        var initialDelay = TimeSpan.FromMinutes(_config.InitialDelayMinutes);
        _logger.LogInformation("Waiting {Minutes} minutes before first evaluation", _config.InitialDelayMinutes);
        await Task.Delay(initialDelay, _timeProvider, stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunWeeklyEvaluationAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Weekly evaluation cancelled (application shutting down)");
                break;
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // Justification: Background service boundary - prevents service crash
            // Background service: Generic catch prevents service from crashing host process
            // Evaluation failure logged but service continues scheduled execution
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running weekly evaluation");
                // Continue to next iteration despite error
            }
#pragma warning restore CA1031

            // Wait for the configured interval before next run
            var interval = TimeSpan.FromDays(_config.IntervalDays);
            _logger.LogInformation("Next weekly evaluation in {Days} days", _config.IntervalDays);
            await Task.Delay(interval, _timeProvider, stoppingToken).ConfigureAwait(false);
        }

        _logger.LogInformation("Weekly evaluation service stopped");
    }

    /// <summary>
    /// Execute comprehensive weekly evaluation.
    /// </summary>
    private async Task RunWeeklyEvaluationAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting weekly quality evaluation");

        using var scope = _scopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // Calculate date range (last N days)
        var endDate = _timeProvider.GetUtcNow().DateTime;
        var startDate = endDate.AddDays(-_config.ReportWindowDays);

        _logger.LogInformation(
            "Evaluating quality metrics from {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
            startDate, endDate);

        // 1. Generate quality report using CQRS query
        var qualityReport = await GenerateQualityReportAsync(mediator, startDate, endDate, cancellationToken).ConfigureAwait(false);

        // 2. Optionally run RAG evaluation if enabled and dataset available
        RagEvaluationReport? ragReport = null;
        if (_config.EnableRagEvaluation && !string.IsNullOrWhiteSpace(_config.RagDatasetPath))
        {
            ragReport = await RunRagEvaluationAsync(scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
        }

        // 3. Check quality thresholds and generate alerts if needed
        await CheckQualityThresholdsAsync(mediator, qualityReport, ragReport, cancellationToken).ConfigureAwait(false);

        // 4. Log summary
        LogEvaluationSummary(qualityReport, ragReport);

        _logger.LogInformation("Weekly quality evaluation completed successfully");
    }

    /// <summary>
    /// Generate quality report using CQRS query handler.
    /// </summary>
    private async Task<QualityReport> GenerateQualityReportAsync(
        IMediator mediator,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Generating quality report via CQRS query");

        var query = new GenerateQualityReportQuery
        {
            StartDate = startDate,
            EndDate = endDate,
            Days = _config.ReportWindowDays
        };

        var report = await mediator.Send(query, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Quality report generated: {TotalResponses} responses, {LowQualityPercentage:F2}% low-quality",
            report.TotalResponses,
            report.LowQualityPercentage);

        return report;
    }

    /// <summary>
    /// Run RAG evaluation if configured.
    /// </summary>
    private async Task<RagEvaluationReport?> RunRagEvaluationAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Running RAG evaluation with dataset: {Dataset}", _config.RagDatasetPath);

            var ragService = serviceProvider.GetService<IRagEvaluationService>();
            if (ragService == null)
            {
                _logger.LogWarning("IRagEvaluationService not available, skipping RAG evaluation");
                return null;
            }

            // Load dataset
            var dataset = await ragService.LoadDatasetAsync(_config.RagDatasetPath!, cancellationToken).ConfigureAwait(false);

            // Run evaluation
            var report = await ragService.EvaluateAsync(
                dataset,
                topK: 10,
                thresholds: null,
                ct: cancellationToken);

            _logger.LogInformation(
                "RAG evaluation completed: MRR={MRR:F4}, P@5={P5:F4}, Latency p95={Latency:F2}ms",
                report.MeanReciprocalRank,
                report.AvgPrecisionAt5,
                report.LatencyP95);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running RAG evaluation, continuing with quality report only");
            return null;
        }
    }

    /// <summary>
    /// Check quality thresholds and send alerts if needed.
    /// </summary>
    private async Task CheckQualityThresholdsAsync(
        IMediator mediator,
        QualityReport qualityReport,
        RagEvaluationReport? ragReport,
        CancellationToken cancellationToken)
    {
        var alerts = new List<string>();

        // Check quality report thresholds
        if (qualityReport.LowQualityPercentage > _config.Thresholds.MaxLowQualityPercentage)
        {
            alerts.Add(
                $"Low-quality percentage ({qualityReport.LowQualityPercentage:F2}%) exceeds threshold ({_config.Thresholds.MaxLowQualityPercentage:F2}%)");
        }

        if (qualityReport.AverageOverallConfidence.HasValue &&
            qualityReport.AverageOverallConfidence.Value < _config.Thresholds.MinOverallConfidence)
        {
            alerts.Add(
                $"Overall confidence ({qualityReport.AverageOverallConfidence.Value:F3}) below threshold ({_config.Thresholds.MinOverallConfidence:F3})");
        }

        if (qualityReport.AverageRagConfidence.HasValue &&
            qualityReport.AverageRagConfidence.Value < _config.Thresholds.MinRagConfidence)
        {
            alerts.Add(
                $"RAG confidence ({qualityReport.AverageRagConfidence.Value:F3}) below threshold ({_config.Thresholds.MinRagConfidence:F3})");
        }

        // Check RAG evaluation quality gates
        if (ragReport != null && !ragReport.PassedQualityGates)
        {
            alerts.Add($"RAG evaluation failed quality gates: {string.Join(", ", ragReport.QualityGateFailures)}");
        }

        // Send alerts if any threshold violations
        if (alerts.Count > 0)
        {
            _logger.LogWarning(
                "Weekly evaluation detected {Count} quality issues: {Issues}",
                alerts.Count,
                string.Join("; ", alerts));

            // Send alert via CQRS command (BGAI-042)
            var alertCommand = new SendAlertCommand(
                AlertType: "QualityEvaluation",
                Severity: "Warning",
                Message: $"Weekly quality evaluation detected {alerts.Count} issue(s)",
                Metadata: new Dictionary<string, object>
(StringComparer.Ordinal)
                {
                    { "Issues", alerts },
                    { "IssueCount", alerts.Count },
                    { "StartDate", qualityReport.StartDate },
                    { "EndDate", qualityReport.EndDate },
                    { "LowQualityPercentage", qualityReport.LowQualityPercentage }
                }
            );
            await mediator.Send(alertCommand, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            _logger.LogInformation("All quality thresholds passed");
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Log comprehensive evaluation summary.
    /// </summary>
    private void LogEvaluationSummary(QualityReport qualityReport, RagEvaluationReport? ragReport)
    {
        _logger.LogInformation(
            "=== Weekly Evaluation Summary ===\n" +
            "Period: {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}\n" +
            "Total Responses: {TotalResponses}\n" +
            "Low Quality: {LowQualityCount} ({LowQualityPercentage:F2}%)\n" +
            "Avg Overall Confidence: {AvgOverall:F3}\n" +
            "Avg RAG Confidence: {AvgRag:F3}\n" +
            "Avg LLM Confidence: {AvgLlm:F3}\n" +
            "Avg Citation Quality: {AvgCitation:F3}",
            qualityReport.StartDate,
            qualityReport.EndDate,
            qualityReport.TotalResponses,
            qualityReport.LowQualityCount,
            qualityReport.LowQualityPercentage,
            qualityReport.AverageOverallConfidence ?? 0.0,
            qualityReport.AverageRagConfidence ?? 0.0,
            qualityReport.AverageLlmConfidence ?? 0.0,
            qualityReport.AverageCitationQuality ?? 0.0);

        if (ragReport != null)
        {
            _logger.LogInformation(
                "=== RAG Evaluation Summary ===\n" +
                "Dataset: {Dataset}\n" +
                "Total Queries: {TotalQueries}\n" +
                "Successful: {Successful}\n" +
                "MRR: {MRR:F4}\n" +
                "Precision@5: {P5:F4}\n" +
                "Recall@K: {RecallK:F4}\n" +
                "Latency p95: {Latency:F2}ms\n" +
                "Quality Gates: {QualityGates}",
                ragReport.DatasetName,
                ragReport.TotalQueries,
                ragReport.SuccessfulQueries,
                ragReport.MeanReciprocalRank,
                ragReport.AvgPrecisionAt5,
                ragReport.AvgRecallAtK,
                ragReport.LatencyP95,
                ragReport.PassedQualityGates ? "PASSED" : "FAILED");
        }
    }
}

using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Quartz;
using System.Text.Json;

namespace Api.BoundedContexts.Administration.Infrastructure.Scheduling;

/// <summary>
/// Quartz.NET job for executing scheduled reports
/// ISSUE-916: Background report generation job
/// </summary>
[DisallowConcurrentExecution]
public sealed class GenerateReportJob : IJob
{
    private readonly IReportGeneratorService _reportGenerator;
    private readonly IAdminReportRepository _reportRepository;
    private readonly IReportExecutionRepository _executionRepository;
    private readonly ILogger<GenerateReportJob> _logger;

    public GenerateReportJob(
        IReportGeneratorService reportGenerator,
        IAdminReportRepository reportRepository,
        IReportExecutionRepository executionRepository,
        ILogger<GenerateReportJob> logger)
    {
        _reportGenerator = reportGenerator ?? throw new ArgumentNullException(nameof(reportGenerator));
        _reportRepository = reportRepository ?? throw new ArgumentNullException(nameof(reportRepository));
        _executionRepository = executionRepository ?? throw new ArgumentNullException(nameof(executionRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var reportId = context.MergedJobDataMap.GetGuidValue("ReportId");

        _logger.LogInformation(
            "Executing scheduled report: ReportId={ReportId}, FireTime={FireTime}",
            reportId, context.FireTimeUtc);

        // Get report definition
        var report = await _reportRepository.GetByIdAsync(reportId, context.CancellationToken)
            .ConfigureAwait(false);

        if (report is null)
        {
            _logger.LogWarning("Report not found: {ReportId}", reportId);
            return;
        }

        if (!report.IsActive)
        {
            _logger.LogInformation("Report is inactive, skipping: {ReportId}", reportId);
            return;
        }

        // Create execution record
        var execution = ReportExecution.Create(reportId);
        await _executionRepository.AddAsync(execution, context.CancellationToken)
            .ConfigureAwait(false);

        try
        {
            // Generate report
            var reportData = await _reportGenerator.GenerateAsync(
                report.Template,
                report.Format,
                report.Parameters,
                context.CancellationToken).ConfigureAwait(false);

            // Update execution as completed
            var completedExecution = execution.Complete(
                outputPath: $"scheduled://{reportData.FileName}",
                fileSizeBytes: reportData.FileSizeBytes);

            await _executionRepository.UpdateAsync(completedExecution, context.CancellationToken)
                .ConfigureAwait(false);

            // Update report last execution timestamp
            var updatedReport = report.WithLastExecutedAt(DateTime.UtcNow);
            await _reportRepository.UpdateAsync(updatedReport, context.CancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Scheduled report generated successfully: ExecutionId={ExecutionId}, FileName={FileName}, Size={Size}",
                completedExecution.Id, reportData.FileName, reportData.FileSizeBytes);

            // Store execution result in job context for monitoring
            context.Result = new
            {
                Success = true,
                ExecutionId = completedExecution.Id,
                FileName = reportData.FileName,
                FileSizeBytes = reportData.FileSizeBytes
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Scheduled report generation failed: ReportId={ReportId}, ExecutionId={ExecutionId}",
                reportId, execution.Id);

            // Update execution as failed
            var failedExecution = execution.Fail(ex.Message);
            await _executionRepository.UpdateAsync(failedExecution, context.CancellationToken)
                .ConfigureAwait(false);

            context.Result = new
            {
                Success = false,
                Error = ex.Message,
                ExecutionId = execution.Id
            };

            // Don't rethrow - Quartz will mark job as failed
        }
    }
}

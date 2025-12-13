using Api.BoundedContexts.Administration.Domain.Entities;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.Administration.Infrastructure.Scheduling;

/// <summary>
/// Quartz.NET implementation of IReportSchedulerService
/// ISSUE-916: Background job scheduling with Quartz
/// </summary>
public sealed class QuartzReportSchedulerService : IReportSchedulerService
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILogger<QuartzReportSchedulerService> _logger;

    public QuartzReportSchedulerService(
        ISchedulerFactory schedulerFactory,
        ILogger<QuartzReportSchedulerService> logger)
    {
        _schedulerFactory = schedulerFactory ?? throw new ArgumentNullException(nameof(schedulerFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ScheduleReportAsync(AdminReport report, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(report.ScheduleExpression))
        {
            throw new ArgumentException("Schedule expression is required", nameof(report));
        }

        _logger.LogInformation(
            "Scheduling report: ReportId={ReportId}, Schedule={Schedule}",
            report.Id, report.ScheduleExpression);

        var scheduler = await _schedulerFactory.GetScheduler(ct).ConfigureAwait(false);

        // Create job
        var jobKey = new JobKey($"report-{report.Id}", "reports");
        var job = JobBuilder.Create<GenerateReportJob>()
            .WithIdentity(jobKey)
            .WithDescription($"{report.Name} - {report.Description}")
            .UsingJobData("ReportId", report.Id.ToString())
            .StoreDurably(true)
            .Build();

        // Create trigger with cron expression
        var triggerKey = new TriggerKey($"report-trigger-{report.Id}", "reports");
        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .WithDescription($"Trigger for {report.Name}")
            .WithCronSchedule(report.ScheduleExpression)
            .ForJob(jobKey)
            .Build();

        // Schedule the job
        await scheduler.ScheduleJob(job, trigger, ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Report scheduled successfully: JobKey={JobKey}",
            jobKey);
    }

    public async Task UnscheduleReportAsync(Guid reportId, CancellationToken ct = default)
    {
        _logger.LogInformation("Unscheduling report: ReportId={ReportId}", reportId);

        var scheduler = await _schedulerFactory.GetScheduler(ct).ConfigureAwait(false);
        var jobKey = new JobKey($"report-{reportId}", "reports");

        var deleted = await scheduler.DeleteJob(jobKey, ct).ConfigureAwait(false);

        if (deleted)
        {
            _logger.LogInformation("Report unscheduled: JobKey={JobKey}", jobKey);
        }
        else
        {
            _logger.LogWarning("Report job not found: JobKey={JobKey}", jobKey);
        }
    }

    public async Task TriggerReportAsync(Guid reportId, CancellationToken ct = default)
    {
        _logger.LogInformation("Triggering immediate report execution: ReportId={ReportId}", reportId);

        var scheduler = await _schedulerFactory.GetScheduler(ct).ConfigureAwait(false);
        var jobKey = new JobKey($"report-{reportId}", "reports");

        await scheduler.TriggerJob(jobKey, ct).ConfigureAwait(false);

        _logger.LogInformation("Report triggered: JobKey={JobKey}", jobKey);
    }

    public async Task<SchedulerStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var scheduler = await _schedulerFactory.GetScheduler(ct).ConfigureAwait(false);
        var metadata = await scheduler.GetMetaData(ct).ConfigureAwait(false);

        return new SchedulerStatus(
            IsRunning: metadata.Started,
            ActiveJobs: metadata.NumberOfJobsExecuted,
            LastExecutionTime: null, // Can be enhanced with job history
            TotalExecutions: metadata.NumberOfJobsExecuted);
    }
}

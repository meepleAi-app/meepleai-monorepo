using Api.BoundedContexts.Administration.Domain.Entities;

namespace Api.BoundedContexts.Administration.Infrastructure.Scheduling;

/// <summary>
/// Service for scheduling reports with Quartz.NET
/// ISSUE-916: Background job scheduling abstraction
/// </summary>
internal interface IReportSchedulerService
{
    /// <summary>
    /// Schedules a report for recurring execution
    /// </summary>
    Task ScheduleReportAsync(AdminReport report, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unschedules a report
    /// </summary>
    Task UnscheduleReportAsync(Guid reportId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggers immediate execution of a scheduled report
    /// </summary>
    Task TriggerReportAsync(Guid reportId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets scheduler status for monitoring
    /// </summary>
    Task<SchedulerStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Scheduler status information
/// </summary>
internal sealed record SchedulerStatus(
    bool IsRunning,
    int ActiveJobs,
    DateTime? LastExecutionTime,
    int TotalExecutions);


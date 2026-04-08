using Api.BoundedContexts.SessionTracking.Domain.Services;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Scheduling;

/// <summary>
/// Quartz.NET implementation of <see cref="IAutoSaveSchedulerService"/>.
/// Dynamically schedules/unschedules per-session auto-save jobs at 60-second intervals.
/// </summary>
internal sealed class QuartzAutoSaveSchedulerService(
    ISchedulerFactory schedulerFactory,
    ILogger<QuartzAutoSaveSchedulerService> logger
) : IAutoSaveSchedulerService
{
    public async Task RegisterAsync(Guid sessionId, CancellationToken ct = default)
    {
        var scheduler = await schedulerFactory.GetScheduler(ct).ConfigureAwait(false);
        var jobKey = AutoSaveSessionJob.GetJobKey(sessionId);

        // Idempotent: skip if already scheduled
        if (await scheduler.CheckExists(jobKey, ct).ConfigureAwait(false))
        {
            logger.LogDebug("Auto-save job already exists for session {SessionId}", sessionId);
            return;
        }

        var job = JobBuilder.Create<AutoSaveSessionJob>()
            .WithIdentity(jobKey)
            .WithDescription($"Auto-save checkpoint for session {sessionId}")
            .UsingJobData(AutoSaveSessionJob.SessionIdKey, sessionId.ToString())
            .StoreDurably(false)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity(AutoSaveSessionJob.GetTriggerKey(sessionId))
            .WithDescription($"Auto-save trigger for session {sessionId}")
            .ForJob(jobKey)
            .WithSimpleSchedule(x => x
                .WithIntervalInSeconds(AutoSaveSessionJob.IntervalSeconds)
                .RepeatForever())
            .StartAt(DateTimeOffset.UtcNow.AddSeconds(AutoSaveSessionJob.IntervalSeconds))
            .Build();

        await scheduler.ScheduleJob(job, trigger, ct).ConfigureAwait(false);

        logger.LogInformation(
            "Auto-save job registered for session {SessionId} (every {Interval}s)",
            sessionId, AutoSaveSessionJob.IntervalSeconds);
    }

    public async Task RemoveAsync(Guid sessionId, CancellationToken ct = default)
    {
        var scheduler = await schedulerFactory.GetScheduler(ct).ConfigureAwait(false);
        var jobKey = AutoSaveSessionJob.GetJobKey(sessionId);

        var deleted = await scheduler.DeleteJob(jobKey, ct).ConfigureAwait(false);

        if (deleted)
        {
            logger.LogInformation("Auto-save job removed for session {SessionId}", sessionId);
        }
        else
        {
            logger.LogDebug("Auto-save job not found for session {SessionId} (already removed)", sessionId);
        }
    }
}

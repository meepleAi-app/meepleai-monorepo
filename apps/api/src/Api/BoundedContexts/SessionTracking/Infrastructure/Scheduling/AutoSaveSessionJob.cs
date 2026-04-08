using Api.BoundedContexts.SessionTracking.Application.Commands;
using MediatR;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Scheduling;

/// <summary>
/// Quartz.NET job that triggers an auto-save checkpoint for a specific active session.
/// Scheduled dynamically per-session (every 60 seconds) via <see cref="QuartzAutoSaveSchedulerService"/>.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class AutoSaveSessionJob(
    IMediator mediator,
    ILogger<AutoSaveSessionJob> logger
) : IJob
{
    /// <summary>
    /// JobDataMap key used to pass the session ID to the job instance.
    /// </summary>
    internal const string SessionIdKey = "SessionId";

    /// <summary>
    /// Quartz group name for all auto-save jobs.
    /// </summary>
    internal const string GroupName = "session-autosave";

    /// <summary>
    /// Auto-save interval in seconds.
    /// </summary>
    internal const int IntervalSeconds = 60;

    public async Task Execute(IJobExecutionContext context)
    {
        var sessionIdStr = context.MergedJobDataMap.GetString(SessionIdKey);
        if (!Guid.TryParse(sessionIdStr, out var sessionId))
        {
            logger.LogWarning("AutoSaveSessionJob: invalid SessionId in job data: {Raw}", sessionIdStr);
            return;
        }

        logger.LogDebug("AutoSaveSessionJob firing for session {SessionId}", sessionId);

        try
        {
            await mediator.Send(new AutoSaveSessionCommand(sessionId), context.CancellationToken)
                .ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Background job must not throw
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AutoSaveSessionJob failed for session {SessionId}", sessionId);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Builds the <see cref="JobKey"/> for a given session.
    /// </summary>
    internal static JobKey GetJobKey(Guid sessionId) =>
        new($"autosave-{sessionId}", GroupName);

    /// <summary>
    /// Builds the <see cref="TriggerKey"/> for a given session.
    /// </summary>
    internal static TriggerKey GetTriggerKey(Guid sessionId) =>
        new($"autosave-trigger-{sessionId}", GroupName);
}

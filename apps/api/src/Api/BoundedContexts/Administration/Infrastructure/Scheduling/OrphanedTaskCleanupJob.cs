using Api.BoundedContexts.Administration.Application.Configuration;
using Api.BoundedContexts.Administration.Domain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Api.BoundedContexts.Administration.Infrastructure.Scheduling;

/// <summary>
/// Quartz.NET job for cleaning up orphaned analysis tasks in Redis.
/// ISSUE-2528: Background job for automatic cleanup of stale tasks
/// </summary>
[DisallowConcurrentExecution]
internal sealed class OrphanedTaskCleanupJob : IJob
{
    private readonly IOrphanedTaskCleanupService _cleanupService;
    private readonly IOptions<OrphanedTaskCleanupOptions> _options;
    private readonly ILogger<OrphanedTaskCleanupJob> _logger;

    public OrphanedTaskCleanupJob(
        IOrphanedTaskCleanupService cleanupService,
        IOptions<OrphanedTaskCleanupOptions> options,
        ILogger<OrphanedTaskCleanupJob> logger)
    {
        _cleanupService = cleanupService ?? throw new ArgumentNullException(nameof(cleanupService));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var config = _options.Value;

        if (!config.Enabled)
        {
            _logger.LogDebug("Orphaned task cleanup job is disabled via configuration");
            return;
        }

        _logger.LogInformation(
            "Starting orphaned task cleanup job: RetentionPeriod={RetentionPeriod}, FireTime={FireTime}",
            config.RetentionPeriod, context.FireTimeUtc);

        try
        {
            var cleanedCount = await _cleanupService.CleanupOrphanedTasksAsync(
                config.RetentionPeriod,
                context.CancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Orphaned task cleanup job completed successfully: CleanedCount={CleanedCount}",
                cleanedCount);

            // Store execution result in job context for monitoring
            context.Result = new
            {
                Success = true,
                CleanedCount = cleanedCount,
                RetentionPeriod = config.RetentionPeriod
            };
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // BACKGROUND SERVICE: BACKGROUND TASK PATTERN - Scheduled task error isolation
        // Background tasks must not throw exceptions (would terminate task scheduler).
        // Errors logged for monitoring; task failures don't impact main application.
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Orphaned task cleanup job failed: RetentionPeriod={RetentionPeriod}",
                config.RetentionPeriod);

            context.Result = new
            {
                Success = false,
                Error = ex.Message
            };

            // Don't rethrow - Quartz will mark job as failed
        }
#pragma warning restore CA1031
    }
}

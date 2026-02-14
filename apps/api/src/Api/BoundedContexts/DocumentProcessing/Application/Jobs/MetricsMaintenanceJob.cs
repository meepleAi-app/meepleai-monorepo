using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Quartz;

namespace Api.BoundedContexts.DocumentProcessing.Application.Jobs;

/// <summary>
/// Background job for PDF processing metrics maintenance.
/// Performs cleanup of old metrics and statistics recalculation.
/// Runs hourly to maintain optimal performance and data freshness.
/// Issue #4212: Historical metrics maintenance
/// </summary>
[DisallowConcurrentExecution]
internal sealed class MetricsMaintenanceJob : IJob
{
    private readonly IProcessingMetricsService _metricsService;
    private readonly ILogger<MetricsMaintenanceJob> _logger;

    // Retain last N records per step for historical analysis
    private const int RetainPerStep = 1000;

    public MetricsMaintenanceJob(
        IProcessingMetricsService metricsService,
        ILogger<MetricsMaintenanceJob> logger)
    {
        _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation(
            "MetricsMaintenanceJob started: FireTime={FireTime}",
            context.FireTimeUtc);

        var cancellationToken = context.CancellationToken;
        var deletedCount = 0;

        try
        {
            // Cleanup old metrics (retain last 1000 per step)
            _logger.LogDebug("Starting metrics cleanup, retaining {RetainCount} per step", RetainPerStep);

            deletedCount = await _metricsService.CleanupOldMetricsAsync(
                RetainPerStep,
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "MetricsMaintenanceJob completed successfully: {DeletedCount} old metrics deleted",
                deletedCount);

            context.Result = new
            {
                Success = true,
                MetricsDeleted = deletedCount,
                RetainedPerStep = RetainPerStep
            };
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // BACKGROUND SERVICE: BACKGROUND TASK PATTERN - Scheduled task error isolation
        // Background tasks must not throw exceptions (would terminate task scheduler).
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "MetricsMaintenanceJob failed after deleting {DeletedCount} records",
                deletedCount);

            context.Result = new
            {
                Success = false,
                Error = ex.Message,
                MetricsDeleted = deletedCount
            };

            // Don't rethrow - Quartz will mark job as failed
        }
#pragma warning restore CA1031
    }
}

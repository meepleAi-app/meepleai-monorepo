using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.Services;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Scheduling;

/// <summary>
/// Quartz.NET job that monitors dead letter email accumulation.
/// Sends admin alert when dead letters exceed threshold.
/// Issue #40: Dead letter monitor with throttled alerts.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class DeadLetterMonitorJob : IJob
{
    private readonly IEmailQueueRepository _emailQueueRepository;
    private readonly IAlertingService _alertingService;
    private readonly ILogger<DeadLetterMonitorJob> _logger;

    private const int DeadLetterThreshold = 10;
    private const string AlertType = "dead_letter_accumulation";

    public DeadLetterMonitorJob(
        IEmailQueueRepository emailQueueRepository,
        IAlertingService alertingService,
        ILogger<DeadLetterMonitorJob> logger)
    {
        _emailQueueRepository = emailQueueRepository ?? throw new ArgumentNullException(nameof(emailQueueRepository));
        _alertingService = alertingService ?? throw new ArgumentNullException(nameof(alertingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogDebug("Starting dead letter monitor job");

        try
        {
            var deadLetterCount = await _emailQueueRepository
                .GetDeadLetterCountAsync(context.CancellationToken)
                .ConfigureAwait(false);

            _logger.LogDebug("Dead letter count: {Count}", deadLetterCount);

            if (deadLetterCount > DeadLetterThreshold)
            {
                // IAlertingService has built-in throttling (1 per hour per alert type)
                await _alertingService.SendAlertAsync(
                    AlertType,
                    "warning",
                    $"\u26a0\ufe0f {deadLetterCount} emails in dead letter queue \u2014 requires attention",
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["deadLetterCount"] = deadLetterCount,
                        ["threshold"] = DeadLetterThreshold
                    },
                    context.CancellationToken).ConfigureAwait(false);

                _logger.LogWarning(
                    "Dead letter threshold exceeded: {Count} > {Threshold}",
                    deadLetterCount, DeadLetterThreshold);
            }

            context.Result = new { Success = true, DeadLetterCount = deadLetterCount };
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Dead letter monitor job failed");
            context.Result = new { Success = false, Error = ex.Message };
        }
    }
}

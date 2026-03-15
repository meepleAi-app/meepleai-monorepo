using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.Services;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Scheduling;

/// <summary>
/// Quartz.NET job that monitors dead letter accumulation for both email and notification queues.
/// Sends admin alert when dead letters exceed threshold.
/// Issue #40: Dead letter monitor with throttled alerts.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class DeadLetterMonitorJob : IJob
{
    private readonly IEmailQueueRepository _emailQueueRepository;
    private readonly INotificationQueueRepository _notificationQueueRepository;
    private readonly IAlertingService _alertingService;
    private readonly ILogger<DeadLetterMonitorJob> _logger;

    private const int DeadLetterThreshold = 10;
    private const string EmailAlertType = "dead_letter_accumulation";
    private const string NotificationQueueAlertType = "notification_queue_dead_letter_accumulation";

    public DeadLetterMonitorJob(
        IEmailQueueRepository emailQueueRepository,
        INotificationQueueRepository notificationQueueRepository,
        IAlertingService alertingService,
        ILogger<DeadLetterMonitorJob> logger)
    {
        _emailQueueRepository = emailQueueRepository ?? throw new ArgumentNullException(nameof(emailQueueRepository));
        _notificationQueueRepository = notificationQueueRepository ?? throw new ArgumentNullException(nameof(notificationQueueRepository));
        _alertingService = alertingService ?? throw new ArgumentNullException(nameof(alertingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogDebug("Starting dead letter monitor job");

        try
        {
            // 1. Monitor email queue dead letters
            var emailDeadLetterCount = await _emailQueueRepository
                .GetDeadLetterCountAsync(context.CancellationToken)
                .ConfigureAwait(false);

            _logger.LogDebug("Email dead letter count: {Count}", emailDeadLetterCount);

            if (emailDeadLetterCount > DeadLetterThreshold)
            {
                await _alertingService.SendAlertAsync(
                    EmailAlertType,
                    "warning",
                    $"\u26a0\ufe0f {emailDeadLetterCount} emails in dead letter queue \u2014 requires attention",
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["deadLetterCount"] = emailDeadLetterCount,
                        ["threshold"] = DeadLetterThreshold,
                        ["queueType"] = "email"
                    },
                    context.CancellationToken).ConfigureAwait(false);

                _logger.LogWarning(
                    "Email dead letter threshold exceeded: {Count} > {Threshold}",
                    emailDeadLetterCount, DeadLetterThreshold);
            }

            // 2. Monitor notification queue dead letters
            var notificationDeadLetterCount = await _notificationQueueRepository
                .GetDeadLetterCountAsync(context.CancellationToken)
                .ConfigureAwait(false);

            _logger.LogDebug("Notification queue dead letter count: {Count}", notificationDeadLetterCount);

            if (notificationDeadLetterCount > DeadLetterThreshold)
            {
                await _alertingService.SendAlertAsync(
                    NotificationQueueAlertType,
                    "warning",
                    $"\u26a0\ufe0f {notificationDeadLetterCount} notifications in dead letter queue \u2014 requires attention",
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["deadLetterCount"] = notificationDeadLetterCount,
                        ["threshold"] = DeadLetterThreshold,
                        ["queueType"] = "notification"
                    },
                    context.CancellationToken).ConfigureAwait(false);

                _logger.LogWarning(
                    "Notification queue dead letter threshold exceeded: {Count} > {Threshold}",
                    notificationDeadLetterCount, DeadLetterThreshold);
            }

            context.Result = new
            {
                Success = true,
                EmailDeadLetterCount = emailDeadLetterCount,
                NotificationDeadLetterCount = notificationDeadLetterCount
            };
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

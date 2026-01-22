using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.SystemConfiguration.Domain.Services;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Scheduling;

/// <summary>
/// Scheduled job that notifies users when their cooldown period has ended.
/// ISSUE-2742: Helpful reminder to encourage re-engagement after rejection cooldown.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class CooldownEndReminderJob : IJob
{
    private readonly IShareRequestRepository _shareRequestRepository;
    private readonly IRateLimitEvaluator _rateLimitEvaluator;
    private readonly INotificationRepository _notificationRepository;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<CooldownEndReminderJob> _logger;

    // Look ahead window - notify users whose cooldown ends within this timeframe
    private static readonly TimeSpan LookAheadWindow = TimeSpan.FromHours(1);

    public CooldownEndReminderJob(
        IShareRequestRepository shareRequestRepository,
        IRateLimitEvaluator rateLimitEvaluator,
        INotificationRepository notificationRepository,
        MeepleAiDbContext dbContext,
        ILogger<CooldownEndReminderJob> logger)
    {
        _shareRequestRepository = shareRequestRepository ?? throw new ArgumentNullException(nameof(shareRequestRepository));
        _rateLimitEvaluator = rateLimitEvaluator ?? throw new ArgumentNullException(nameof(rateLimitEvaluator));
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var cancellationToken = context.CancellationToken;

        try
        {
            _logger.LogInformation("CooldownEndReminderJob started");

            var now = DateTime.UtcNow;
            var windowEnd = now.Add(LookAheadWindow);

            // Get all rejected requests to find users who may be in cooldown
            var rejectedRequests = await _shareRequestRepository
                .GetByStatusAsync(ShareRequestStatus.Rejected, cancellationToken)
                .ConfigureAwait(false);

            // Group by user and get the most recent rejection per user
            var usersWithRecentRejections = rejectedRequests
                .Where(r => r.ResolvedAt.HasValue)
                .GroupBy(r => r.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    LastRejectionAt = g.Max(r => r.ResolvedAt!.Value)
                })
                .ToList();

            var remindersCreated = 0;

            foreach (var userInfo in usersWithRecentRejections)
            {
                // Check current rate limit status
                var status = await _rateLimitEvaluator.GetUserStatusAsync(userInfo.UserId, cancellationToken)
                    .ConfigureAwait(false);

                // Skip if no cooldown or cooldown already ended
                if (!status.CooldownEndsAt.HasValue)
                {
                    continue;
                }

                // Check if cooldown ends within the look-ahead window
                if (status.CooldownEndsAt.Value > now && status.CooldownEndsAt.Value <= windowEnd)
                {
                    // Cooldown is about to end - create reminder notification
                    var notification = new Notification(
                        id: Guid.NewGuid(),
                        userId: userInfo.UserId,
                        type: NotificationType.CooldownEnded,
                        severity: NotificationSeverity.Success,
                        title: "Ready to Contribute Again! 🎉",
                        message: "Your cooldown period has ended. You can now submit new share requests.",
                        link: "/library",
                        metadata: System.Text.Json.JsonSerializer.Serialize(new
                        {
                            cooldownEndedAt = status.CooldownEndsAt.Value,
                            remainingMonthly = status.RemainingMonthlyRequests,
                            remainingPending = status.RemainingPendingRequests,
                            tier = status.Tier.ToString()
                        }));

                    await _notificationRepository.AddAsync(notification, cancellationToken)
                        .ConfigureAwait(false);

                    remindersCreated++;

                    _logger.LogInformation(
                        "Created cooldown end reminder for user {UserId}. Cooldown ends at {CooldownEndsAt}",
                        userInfo.UserId,
                        status.CooldownEndsAt.Value);
                }
            }

            if (remindersCreated > 0)
            {
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            _logger.LogInformation(
                "CooldownEndReminderJob completed. Created {Count} reminders",
                remindersCreated);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // SERVICE BOUNDARY: SCHEDULED JOB PATTERN - Background processing
        // Jobs must handle all exceptions internally to avoid Quartz scheduler disruption.
        // Errors logged for monitoring; job failures should not cascade to other jobs.
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex, "CooldownEndReminderJob failed");
            // Don't rethrow - job exceptions would disrupt the Quartz scheduler
        }
#pragma warning restore CA1031
    }
}

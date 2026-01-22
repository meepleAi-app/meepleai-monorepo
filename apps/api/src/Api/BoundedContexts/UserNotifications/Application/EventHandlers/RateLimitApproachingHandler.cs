using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SystemConfiguration.Domain.Services;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// Event handler that creates notifications when users approach their rate limits.
/// ISSUE-2742: Proactive warning notifications at 80% threshold for monthly and pending limits.
/// </summary>
internal sealed class RateLimitApproachingHandler : INotificationHandler<ShareRequestCreatedEvent>
{
    private readonly IRateLimitEvaluator _rateLimitEvaluator;
    private readonly INotificationRepository _notificationRepository;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<RateLimitApproachingHandler> _logger;

    // Threshold percentage for triggering warnings (80%)
    private const decimal WarningThreshold = 80.0m;

    public RateLimitApproachingHandler(
        IRateLimitEvaluator rateLimitEvaluator,
        INotificationRepository notificationRepository,
        MeepleAiDbContext dbContext,
        ILogger<RateLimitApproachingHandler> logger)
    {
        _rateLimitEvaluator = rateLimitEvaluator ?? throw new ArgumentNullException(nameof(rateLimitEvaluator));
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(ShareRequestCreatedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var status = await _rateLimitEvaluator.GetUserStatusAsync(notification.UserId, cancellationToken)
                .ConfigureAwait(false);

            // Admin users have unlimited access - no warnings needed
            if (status.HasUnlimitedAccess)
            {
                return;
            }

            // Check if approaching monthly limit (80% threshold)
            if (status.MonthlyUsagePercent >= WarningThreshold && status.MonthlyUsagePercent < 100)
            {
                var monthlyWarning = new Notification(
                    id: Guid.NewGuid(),
                    userId: notification.UserId,
                    type: NotificationType.RateLimitApproaching,
                    severity: NotificationSeverity.Warning,
                    title: "Approaching Monthly Limit",
                    message: $"You've used {status.CurrentMonthlyCount} of your {status.EffectiveMaxPerMonth} monthly share requests. " +
                             $"Your limit resets on {status.MonthResetAt:MMMM d}.",
                    link: "/contributions",
                    metadata: System.Text.Json.JsonSerializer.Serialize(new
                    {
                        limitType = "monthly",
                        currentCount = status.CurrentMonthlyCount,
                        maxCount = status.EffectiveMaxPerMonth,
                        usagePercent = status.MonthlyUsagePercent,
                        resetAt = status.MonthResetAt,
                        tier = status.Tier.ToString()
                    }));

                await _notificationRepository.AddAsync(monthlyWarning, cancellationToken)
                    .ConfigureAwait(false);

                _logger.LogInformation(
                    "Created monthly rate limit warning for user {UserId}: {CurrentCount}/{MaxCount} ({UsagePercent}%)",
                    notification.UserId,
                    status.CurrentMonthlyCount,
                    status.EffectiveMaxPerMonth,
                    status.MonthlyUsagePercent);
            }

            // Check if approaching pending limit (80% threshold)
            if (status.PendingUsagePercent >= WarningThreshold && status.PendingUsagePercent < 100)
            {
                var pendingWarning = new Notification(
                    id: Guid.NewGuid(),
                    userId: notification.UserId,
                    type: NotificationType.RateLimitApproaching,
                    severity: NotificationSeverity.Warning,
                    title: "Approaching Pending Limit",
                    message: $"You have {status.CurrentPendingCount} pending requests. " +
                             $"You can have up to {status.EffectiveMaxPending} pending at once.",
                    link: "/contributions/requests?status=pending",
                    metadata: System.Text.Json.JsonSerializer.Serialize(new
                    {
                        limitType = "pending",
                        currentCount = status.CurrentPendingCount,
                        maxCount = status.EffectiveMaxPending,
                        usagePercent = status.PendingUsagePercent,
                        tier = status.Tier.ToString()
                    }));

                await _notificationRepository.AddAsync(pendingWarning, cancellationToken)
                    .ConfigureAwait(false);

                _logger.LogInformation(
                    "Created pending rate limit warning for user {UserId}: {CurrentCount}/{MaxCount} ({UsagePercent}%)",
                    notification.UserId,
                    status.CurrentPendingCount,
                    status.EffectiveMaxPending,
                    status.PendingUsagePercent);
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // SERVICE BOUNDARY: EVENT HANDLER PATTERN - Background event processing
        // Event handlers must not throw exceptions (violates mediator/event pattern).
        // Errors logged for monitoring; rate limit warning failures don't block share request creation.
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to create rate limit warning for user {UserId}",
                notification.UserId);
            // Don't rethrow - event handler failures should not block the main operation
        }
#pragma warning restore CA1031
    }
}

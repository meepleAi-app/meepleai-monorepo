using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SystemConfiguration.Domain.Services;
using Api.BoundedContexts.UserNotifications.Application.Constants;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// Event handler that creates notifications when users approach their rate limits.
/// Dispatches via NotificationDispatcher for multi-channel delivery.
/// ISSUE-2742: Proactive warning notifications at 80% threshold for monthly and pending limits.
/// </summary>
internal sealed class RateLimitApproachingHandler : INotificationHandler<ShareRequestCreatedEvent>
{
    private readonly IRateLimitEvaluator _rateLimitEvaluator;
    private readonly INotificationDispatcher _dispatcher;
    private readonly ILogger<RateLimitApproachingHandler> _logger;

    // Threshold percentage for triggering warnings (80%)
    private const decimal WarningThreshold = 80.0m;

    public RateLimitApproachingHandler(
        IRateLimitEvaluator rateLimitEvaluator,
        INotificationDispatcher dispatcher,
        ILogger<RateLimitApproachingHandler> logger)
    {
        _rateLimitEvaluator = rateLimitEvaluator ?? throw new ArgumentNullException(nameof(rateLimitEvaluator));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
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

            var nearsMonthly = status.MonthlyUsagePercent >= WarningThreshold && status.MonthlyUsagePercent < 100;
            var nearsPending = status.PendingUsagePercent >= WarningThreshold && status.PendingUsagePercent < 100;
            if (!nearsMonthly && !nearsPending)
            {
                return;
            }

            // #5112: one warning per event. The dispatcher keeps one notification per (recipient, SourceEventId), so a
            // second warning for the same event, to the same user, was dropped as a duplicate.
            var sentences = new List<string>(2);
            if (nearsMonthly)
            {
                sentences.Add(
                    $"You've used {status.CurrentMonthlyCount} of your {status.EffectiveMaxPerMonth} monthly share requests. " +
                    $"Your limit resets on {status.MonthResetAt:MMMM d}.");
            }

            if (nearsPending)
            {
                sentences.Add(
                    $"You have {status.CurrentPendingCount} pending requests. " +
                    $"You can have up to {status.EffectiveMaxPending} pending at once.");
            }

            var title = (nearsMonthly, nearsPending) switch
            {
                (true, true) => "Approaching Your Limits",
                (true, false) => "Approaching Monthly Limit",
                _ => "Approaching Pending Limit",
            };

            await _dispatcher.DispatchAsync(new NotificationMessage
            {
                Type = NotificationType.RateLimitApproaching,
                RecipientUserId = notification.UserId,
                Payload = new GenericPayload(title, string.Join(" ", sentences)),
                // The pending requests are the ones a user can act on now (withdraw, complete): their page wins.
                DeepLinkPath = nearsPending ? NotificationRoutes.ContributionsPending : NotificationRoutes.Contributions,
                // Issue #1937 / CF-1: propagate event id for dispatcher dedup
                SourceEventId = notification.EventId
            }, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Dispatched rate limit warning for user {UserId}: monthly {MonthlyCount}/{MonthlyMax} ({MonthlyPercent}%), pending {PendingCount}/{PendingMax} ({PendingPercent}%)",
                notification.UserId,
                status.CurrentMonthlyCount,
                status.EffectiveMaxPerMonth,
                status.MonthlyUsagePercent,
                status.CurrentPendingCount,
                status.EffectiveMaxPending,
                status.PendingUsagePercent);
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

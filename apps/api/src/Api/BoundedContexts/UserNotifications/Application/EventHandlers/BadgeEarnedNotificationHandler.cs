using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Application.Constants;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// Event handler that creates celebratory notifications when users earn badges.
/// Dispatches via NotificationDispatcher for multi-channel delivery (in-app, email, Slack).
/// ISSUE-2741: Badge earned notification system; a milestone badge adds its own celebration.
/// </summary>
/// <remarks>
/// #5110: the milestone celebration used to be a second handler of the same event. Both dispatched with the event's
/// id as SourceEventId, the dispatcher keeps one notification per (user, SourceEventId), and which of the two survived
/// depended on the order MediatR ran them in. One notification, with the milestone text when there is one.
/// </remarks>
internal sealed class BadgeEarnedNotificationHandler : INotificationHandler<BadgeEarnedEvent>
{
    private static readonly HashSet<string> MilestoneBadgeCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "CONTRIBUTOR_50",
        "CONTRIBUTOR_100",
        "FIRST_CONTRIBUTION",
        "COMMUNITY_CHAMPION"
    };

    private readonly INotificationDispatcher _dispatcher;
    private readonly IBadgeRepository _badgeRepository;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<BadgeEarnedNotificationHandler> _logger;

    public BadgeEarnedNotificationHandler(
        INotificationDispatcher dispatcher,
        IBadgeRepository badgeRepository,
        MeepleAiDbContext dbContext,
        ILogger<BadgeEarnedNotificationHandler> logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _badgeRepository = badgeRepository ?? throw new ArgumentNullException(nameof(badgeRepository));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(BadgeEarnedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            // Get badge details
            var badge = await _badgeRepository.GetByIdAsync(notification.BadgeId, cancellationToken)
                .ConfigureAwait(false);

            if (badge == null)
            {
                _logger.LogWarning(
                    "Badge {BadgeId} not found for notification. Skipping badge earned notification for user {UserId}",
                    notification.BadgeId,
                    notification.UserId);
                return;
            }

            // Get user details
            var user = await _dbContext.Set<UserEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == notification.UserId, cancellationToken)
                .ConfigureAwait(false);

            if (user == null)
            {
                _logger.LogWarning(
                    "User {UserId} not found for badge earned notification",
                    notification.UserId);
                return;
            }

            await _dispatcher.DispatchAsync(new NotificationMessage
            {
                Type = NotificationType.BadgeEarned,
                RecipientUserId = notification.UserId,
                Payload = new BadgePayload(
                    notification.BadgeId,
                    badge.Name,
                    MilestoneBadgeCodes.Contains(notification.BadgeCode)
                        ? $"{badge.Description} {GetMilestoneMessage(notification.BadgeCode)}"
                        : badge.Description),
                DeepLinkPath = NotificationRoutes.UserBadges,
                // Issue #1937 / CF-1: propagate event id for dispatcher dedup
                SourceEventId = notification.EventId
            }, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Dispatched badge earned notification for user {UserId} - Badge: {BadgeName} ({BadgeTier})",
                notification.UserId,
                badge.Name,
                badge.Tier);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to create badge earned notification for user {UserId}, badge {BadgeId}",
                notification.UserId,
                notification.BadgeId);
            // Don't rethrow - event handler failures should not block the main operation
        }
#pragma warning restore CA1031
    }

    private static string GetMilestoneMessage(string badgeCode) => badgeCode.ToUpperInvariant() switch
    {
        "FIRST_CONTRIBUTION" => "Welcome to the MeepleAI community! Your first contribution is the beginning of an amazing journey.",
        "CONTRIBUTOR_50" => "50 contributions is a remarkable achievement! You're in the top tier of contributors.",
        "CONTRIBUTOR_100" => "100 contributions! You're a true legend of the MeepleAI community!",
        "COMMUNITY_CHAMPION" => "You've shown exceptional dedication to the community. Thank you for your contributions!",
        _ => "Congratulations on this amazing milestone!"
    };
}

using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// Event handler that creates special notifications for milestone badge achievements.
/// Dispatches via NotificationDispatcher for multi-channel delivery.
/// ISSUE-2741: Extra celebration for milestone badges (50, 100 contributions, etc.)
/// </summary>
internal sealed class MilestoneBadgeNotificationHandler : INotificationHandler<BadgeEarnedEvent>
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly IBadgeRepository _badgeRepository;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<MilestoneBadgeNotificationHandler> _logger;

    // Milestone badge codes that trigger extra celebration
    private static readonly HashSet<string> MilestoneBadgeCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "CONTRIBUTOR_50",
        "CONTRIBUTOR_100",
        "FIRST_CONTRIBUTION",
        "COMMUNITY_CHAMPION"
    };

    public MilestoneBadgeNotificationHandler(
        INotificationDispatcher dispatcher,
        IBadgeRepository badgeRepository,
        MeepleAiDbContext dbContext,
        ILogger<MilestoneBadgeNotificationHandler> logger)
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
            // Check if this is a milestone badge
            if (!MilestoneBadgeCodes.Contains(notification.BadgeCode))
            {
                _logger.LogDebug(
                    "Badge {BadgeCode} is not a milestone badge. Skipping milestone notification.",
                    notification.BadgeCode);
                return;
            }

            // Get badge details
            var badge = await _badgeRepository.GetByIdAsync(notification.BadgeId, cancellationToken)
                .ConfigureAwait(false);

            if (badge == null)
            {
                _logger.LogWarning(
                    "Badge {BadgeId} not found for milestone notification",
                    notification.BadgeId);
                return;
            }

            var user = await _dbContext.Set<UserEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == notification.UserId, cancellationToken)
                .ConfigureAwait(false);

            if (user == null)
            {
                _logger.LogWarning(
                    "User {UserId} not found for milestone badge notification",
                    notification.UserId);
                return;
            }

            var milestoneMessage = GetMilestoneMessage(notification.BadgeCode);

            await _dispatcher.DispatchAsync(new NotificationMessage
            {
                Type = NotificationType.BadgeEarned,
                RecipientUserId = notification.UserId,
                Payload = new BadgePayload(
                    notification.BadgeId,
                    badge.Name,
                    $"{badge.Description} {milestoneMessage}"),
                DeepLinkPath = "/users/me/badges"
            }, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Dispatched milestone badge notification for user {UserId} - Badge: {BadgeName} ({BadgeCode})",
                notification.UserId,
                badge.Name,
                notification.BadgeCode);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to create milestone badge notification for user {UserId}, badge {BadgeCode}",
                notification.UserId,
                notification.BadgeCode);
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

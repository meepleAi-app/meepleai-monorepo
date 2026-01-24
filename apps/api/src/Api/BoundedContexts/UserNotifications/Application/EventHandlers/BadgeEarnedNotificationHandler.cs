using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// Event handler that creates celebratory notifications when users earn badges.
/// ISSUE-2741: Badge earned notification system with in-app and email alerts.
/// </summary>
internal sealed class BadgeEarnedNotificationHandler : INotificationHandler<BadgeEarnedEvent>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IBadgeRepository _badgeRepository;
    private readonly MeepleAiDbContext _dbContext;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BadgeEarnedNotificationHandler> _logger;

    public BadgeEarnedNotificationHandler(
        INotificationRepository notificationRepository,
        IBadgeRepository badgeRepository,
        MeepleAiDbContext dbContext,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<BadgeEarnedNotificationHandler> logger)
    {
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
        _badgeRepository = badgeRepository ?? throw new ArgumentNullException(nameof(badgeRepository));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
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

            // Create in-app notification (celebratory with modal display)
            var inAppNotification = new Notification(
                id: Guid.NewGuid(),
                userId: notification.UserId,
                type: NotificationType.BadgeEarned,
                severity: NotificationSeverity.Success,
                title: GetCelebratoryTitle(badge.Tier),
                message: $"You've earned the \"{badge.Name}\" badge! {badge.Description}",
                link: "/users/me/badges",
                metadata: System.Text.Json.JsonSerializer.Serialize(new
                {
                    badgeId = notification.BadgeId,
                    badgeCode = notification.BadgeCode,
                    badgeName = badge.Name,
                    badgeDescription = badge.Description,
                    badgeIconUrl = badge.IconUrl,
                    badgeTier = badge.Tier.ToString(),
                    displayType = "Modal",  // Show as modal, not just toast
                    showAnimation = true,
                    shareEnabled = true
                }));

            await _notificationRepository.AddAsync(inAppNotification, cancellationToken)
                .ConfigureAwait(false);

            // Send email notification (celebratory)
            // Note: In future, check user.Preferences.ReceiveBadgeEmails when preferences system is implemented (#2742)
#pragma warning disable S1075 // URIs should not be hardcoded - Default/Fallback value
            var baseUrl = _configuration["App:BaseUrl"] ?? "https://meepleai.com";
#pragma warning restore S1075

            await _emailService.SendBadgeEarnedEmailAsync(
                toEmail: user.Email,
                userName: user.DisplayName,
                badgeName: badge.Name,
                badgeDescription: badge.Description,
                badgeIconUrl: badge.IconUrl,
                badgeTier: badge.Tier.ToString(),
                badgeTierColor: GetTierColor(badge.Tier),
                profileUrl: $"{baseUrl}/users/{user.Id}/badges",
                shareText: $"I just earned the \"{badge.Name}\" badge on MeepleAI!",
                cancellationToken)
                .ConfigureAwait(false);

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Created badge earned notification for user {UserId} - Badge: {BadgeName} ({BadgeTier})",
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

    private static string GetCelebratoryTitle(BadgeTier tier) => tier switch
    {
        BadgeTier.Bronze => "Badge Earned! 🥉",
        BadgeTier.Silver => "Badge Earned! 🥈",
        BadgeTier.Gold => "Badge Earned! 🥇",
        BadgeTier.Platinum => "Amazing Achievement! ⭐",
        BadgeTier.Diamond => "Legendary Badge! 💎",
        _ => "Badge Earned! 🎉"
    };

    private static string GetTierColor(BadgeTier tier) => tier switch
    {
        BadgeTier.Bronze => "#CD7F32",
        BadgeTier.Silver => "#C0C0C0",
        BadgeTier.Gold => "#FFD700",
        BadgeTier.Platinum => "#E5E4E2",
        BadgeTier.Diamond => "#B9F2FF",
        _ => "#6B7280"
    };
}

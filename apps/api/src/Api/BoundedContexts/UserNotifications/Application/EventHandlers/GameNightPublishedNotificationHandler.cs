using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// Handles GameNightPublishedEvent to notify each invited user via in-app notification and email.
/// Issue #44/#47: Game night notification types.
/// </summary>
internal sealed class GameNightPublishedNotificationHandler : INotificationHandler<GameNightPublishedEvent>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly INotificationPreferencesRepository _preferencesRepository;
    private readonly IUserRepository _userRepository;
    private readonly IGameNightEmailService _emailService;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GameNightPublishedNotificationHandler> _logger;

    public GameNightPublishedNotificationHandler(
        INotificationRepository notificationRepository,
        INotificationPreferencesRepository preferencesRepository,
        IUserRepository userRepository,
        IGameNightEmailService emailService,
        MeepleAiDbContext dbContext,
        ILogger<GameNightPublishedNotificationHandler> logger)
    {
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
        _preferencesRepository = preferencesRepository ?? throw new ArgumentNullException(nameof(preferencesRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(GameNightPublishedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var correlationId = Guid.NewGuid();

            // Resolve organizer name
            var organizer = await _userRepository.GetByIdAsync(notification.OrganizerId, cancellationToken).ConfigureAwait(false);
            var organizerName = organizer?.DisplayName ?? "Someone";

            foreach (var invitedUserId in notification.InvitedUserIds)
            {
                try
                {
                    var prefs = await _preferencesRepository.GetByUserIdAsync(invitedUserId, cancellationToken).ConfigureAwait(false);

                    // In-app notification (default if no preferences exist)
                    if (prefs == null || prefs.InAppOnGameNightInvitation)
                    {
                        var inAppNotification = new Notification(
                            id: Guid.NewGuid(),
                            userId: invitedUserId,
                            type: NotificationType.GameNightInvitation,
                            severity: NotificationSeverity.Info,
                            title: "Game Night Invitation",
                            message: $"{organizerName} invited you to \"{notification.Title}\"",
                            link: $"/game-nights/{notification.GameNightEventId}",
                            metadata: JsonSerializer.Serialize(new Dictionary<string, object>(StringComparer.Ordinal)
                            {
                                ["gameNightEventId"] = notification.GameNightEventId,
                                ["organizerId"] = notification.OrganizerId,
                                ["scheduledAt"] = notification.ScheduledAt
                            }),
                            correlationId: correlationId);

                        await _notificationRepository.AddAsync(inAppNotification, cancellationToken).ConfigureAwait(false);
                    }

                    // Email notification
                    if (prefs == null || prefs.EmailOnGameNightInvitation)
                    {
                        var user = await _userRepository.GetByIdAsync(invitedUserId, cancellationToken).ConfigureAwait(false);
                        if (user != null)
                        {
                            await _emailService.SendGameNightInvitationEmailAsync(
                                toEmail: user.Email,
                                organizerName: organizerName,
                                title: notification.Title,
                                scheduledAt: notification.ScheduledAt,
                                location: null, // Location not available on the published event
                                gameNames: [],
                                rsvpAcceptUrl: $"/game-nights/{notification.GameNightEventId}/rsvp?action=accept",
                                rsvpDeclineUrl: $"/game-nights/{notification.GameNightEventId}/rsvp?action=decline",
                                unsubscribeUrl: "/settings/notifications",
                                ct: cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
#pragma warning disable CA1031
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to send game night invitation notification to user {UserId} for event {EventId}",
                        invitedUserId, notification.GameNightEventId);
                }
#pragma warning restore CA1031
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Game night published notifications sent for event {EventId} to {Count} invited users (correlationId: {CorrelationId})",
                notification.GameNightEventId, notification.InvitedUserIds.Count, correlationId);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to handle GameNightPublishedEvent for event {EventId}",
                notification.GameNightEventId);
        }
#pragma warning restore CA1031
    }
}

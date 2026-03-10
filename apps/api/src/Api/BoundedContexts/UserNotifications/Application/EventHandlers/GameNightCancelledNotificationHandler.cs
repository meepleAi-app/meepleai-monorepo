using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
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
/// Handles GameNightCancelledEvent to notify ALL invitees via in-app + email.
/// Issue #47: Game night cancellation notifications.
/// </summary>
internal sealed class GameNightCancelledNotificationHandler : INotificationHandler<GameNightCancelledEvent>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly INotificationPreferencesRepository _preferencesRepository;
    private readonly IUserRepository _userRepository;
    private readonly IGameNightEventRepository _gameNightEventRepository;
    private readonly IGameNightEmailService _emailService;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GameNightCancelledNotificationHandler> _logger;

    public GameNightCancelledNotificationHandler(
        INotificationRepository notificationRepository,
        INotificationPreferencesRepository preferencesRepository,
        IUserRepository userRepository,
        IGameNightEventRepository gameNightEventRepository,
        IGameNightEmailService emailService,
        MeepleAiDbContext dbContext,
        ILogger<GameNightCancelledNotificationHandler> logger)
    {
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
        _preferencesRepository = preferencesRepository ?? throw new ArgumentNullException(nameof(preferencesRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _gameNightEventRepository = gameNightEventRepository ?? throw new ArgumentNullException(nameof(gameNightEventRepository));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(GameNightCancelledEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var correlationId = Guid.NewGuid();

            // Resolve organizer name
            var organizer = await _userRepository.GetByIdAsync(notification.OrganizerId, cancellationToken).ConfigureAwait(false);
            var organizerName = organizer?.DisplayName ?? "The organizer";

            // Load the full event to get ScheduledAt (not on the cancelled event)
            var gameNightEvent = await _gameNightEventRepository.GetByIdAsync(notification.GameNightEventId, cancellationToken).ConfigureAwait(false);
            var scheduledAt = gameNightEvent?.ScheduledAt ?? DateTimeOffset.UtcNow;

            foreach (var invitedUserId in notification.InvitedUserIds)
            {
                try
                {
                    var prefs = await _preferencesRepository.GetByUserIdAsync(invitedUserId, cancellationToken).ConfigureAwait(false);

                    // In-app notification
                    if (prefs == null || prefs.InAppOnGameNightInvitation)
                    {
                        var inAppNotification = new Notification(
                            id: Guid.NewGuid(),
                            userId: invitedUserId,
                            type: NotificationType.GameNightCancelled,
                            severity: NotificationSeverity.Warning,
                            title: "Game Night Cancelled",
                            message: $"{organizerName} cancelled \"{notification.Title}\"",
                            link: $"/game-nights/{notification.GameNightEventId}",
                            metadata: JsonSerializer.Serialize(new Dictionary<string, object>(StringComparer.Ordinal)
                            {
                                ["gameNightEventId"] = notification.GameNightEventId,
                                ["organizerId"] = notification.OrganizerId
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
                            await _emailService.SendGameNightCancelledEmailAsync(
                                toEmail: user.Email,
                                organizerName: organizerName,
                                title: notification.Title,
                                scheduledAt: scheduledAt,
                                unsubscribeUrl: "/settings/notifications",
                                ct: cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
#pragma warning disable CA1031
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to send cancellation notification to user {UserId} for event {EventId}",
                        invitedUserId, notification.GameNightEventId);
                }
#pragma warning restore CA1031
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Game night cancelled notifications sent for event {EventId} to {Count} invitees (correlationId: {CorrelationId})",
                notification.GameNightEventId, notification.InvitedUserIds.Count, correlationId);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to handle GameNightCancelledEvent for event {EventId}",
                notification.GameNightEventId);
        }
#pragma warning restore CA1031
    }
}

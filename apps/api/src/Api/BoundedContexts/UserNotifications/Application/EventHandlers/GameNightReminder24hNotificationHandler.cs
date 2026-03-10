using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
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
/// Handles GameNightReminder24hEvent to send 24-hour reminders to accepted and pending RSVPs.
/// Accepted users: in-app + email. Pending users: email with "respond" CTA.
/// Issue #44: Game night reminder notifications.
/// </summary>
internal sealed class GameNightReminder24hNotificationHandler : INotificationHandler<GameNightReminder24hEvent>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly INotificationPreferencesRepository _preferencesRepository;
    private readonly IUserRepository _userRepository;
    private readonly IGameNightEventRepository _gameNightEventRepository;
    private readonly IGameNightEmailService _emailService;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GameNightReminder24hNotificationHandler> _logger;

    public GameNightReminder24hNotificationHandler(
        INotificationRepository notificationRepository,
        INotificationPreferencesRepository preferencesRepository,
        IUserRepository userRepository,
        IGameNightEventRepository gameNightEventRepository,
        IGameNightEmailService emailService,
        MeepleAiDbContext dbContext,
        ILogger<GameNightReminder24hNotificationHandler> logger)
    {
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
        _preferencesRepository = preferencesRepository ?? throw new ArgumentNullException(nameof(preferencesRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _gameNightEventRepository = gameNightEventRepository ?? throw new ArgumentNullException(nameof(gameNightEventRepository));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(GameNightReminder24hEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var correlationId = Guid.NewGuid();

            // Load the full event with RSVPs
            var gameNightEvent = await _gameNightEventRepository.GetByIdAsync(notification.GameNightEventId, cancellationToken).ConfigureAwait(false);
            if (gameNightEvent == null)
            {
                _logger.LogWarning("GameNightEvent {EventId} not found for 24h reminder", notification.GameNightEventId);
                return;
            }

            var confirmedCount = gameNightEvent.AcceptedCount;

            foreach (var rsvp in gameNightEvent.Rsvps)
            {
                // Only notify Accepted and Pending users
                if (rsvp.Status == RsvpStatus.Declined)
                    continue;

                try
                {
                    var prefs = await _preferencesRepository.GetByUserIdAsync(rsvp.UserId, cancellationToken).ConfigureAwait(false);
                    var isAccepted = rsvp.Status == RsvpStatus.Accepted;
                    var isPending = rsvp.Status == RsvpStatus.Pending || rsvp.Status == RsvpStatus.Maybe;

                    // In-app notification for accepted users
                    if (isAccepted && (prefs == null || prefs.InAppOnGameNightInvitation))
                    {
                        var inAppNotification = new Notification(
                            id: Guid.NewGuid(),
                            userId: rsvp.UserId,
                            type: NotificationType.GameNightReminder24h,
                            severity: NotificationSeverity.Info,
                            title: "Game Night Tomorrow!",
                            message: $"\"{notification.Title}\" is happening tomorrow. {confirmedCount} player(s) confirmed.",
                            link: $"/game-nights/{notification.GameNightEventId}",
                            metadata: JsonSerializer.Serialize(new Dictionary<string, object>(StringComparer.Ordinal)
                            {
                                ["gameNightEventId"] = notification.GameNightEventId,
                                ["confirmedCount"] = confirmedCount
                            }),
                            correlationId: correlationId);

                        await _notificationRepository.AddAsync(inAppNotification, cancellationToken).ConfigureAwait(false);
                    }

                    // Email for both accepted and pending users
                    if (prefs == null || prefs.EmailOnGameNightReminder)
                    {
                        var user = await _userRepository.GetByIdAsync(rsvp.UserId, cancellationToken).ConfigureAwait(false);
                        if (user != null)
                        {
                            await _emailService.SendGameNightReminder24hEmailAsync(
                                toEmail: user.Email,
                                title: notification.Title,
                                scheduledAt: notification.ScheduledAt,
                                location: notification.Location,
                                confirmedCount: confirmedCount,
                                isPending: isPending,
                                unsubscribeUrl: "/settings/notifications",
                                ct: cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
#pragma warning disable CA1031
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to send 24h reminder to user {UserId} for event {EventId}",
                        rsvp.UserId, notification.GameNightEventId);
                }
#pragma warning restore CA1031
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "24h reminder notifications sent for event {EventId} (correlationId: {CorrelationId})",
                notification.GameNightEventId, correlationId);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to handle GameNightReminder24hEvent for event {EventId}",
                notification.GameNightEventId);
        }
#pragma warning restore CA1031
    }
}

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
/// Handles GameNightReminder1hEvent to send 1-hour reminders.
/// In-app notification only to accepted users (no email — too close to event time).
/// Issue #44: Game night reminder notifications.
/// </summary>
internal sealed class GameNightReminder1hNotificationHandler : INotificationHandler<GameNightReminder1hEvent>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly INotificationPreferencesRepository _preferencesRepository;
    private readonly IGameNightEventRepository _gameNightEventRepository;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GameNightReminder1hNotificationHandler> _logger;

    public GameNightReminder1hNotificationHandler(
        INotificationRepository notificationRepository,
        INotificationPreferencesRepository preferencesRepository,
        IGameNightEventRepository gameNightEventRepository,
        MeepleAiDbContext dbContext,
        ILogger<GameNightReminder1hNotificationHandler> logger)
    {
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
        _preferencesRepository = preferencesRepository ?? throw new ArgumentNullException(nameof(preferencesRepository));
        _gameNightEventRepository = gameNightEventRepository ?? throw new ArgumentNullException(nameof(gameNightEventRepository));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(GameNightReminder1hEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var correlationId = Guid.NewGuid();

            // Load the full event with RSVPs
            var gameNightEvent = await _gameNightEventRepository.GetByIdAsync(notification.GameNightEventId, cancellationToken).ConfigureAwait(false);
            if (gameNightEvent == null)
            {
                _logger.LogWarning("GameNightEvent {EventId} not found for 1h reminder", notification.GameNightEventId);
                return;
            }

            var notifiedCount = 0;

            foreach (var rsvp in gameNightEvent.Rsvps)
            {
                // Only notify accepted users for 1h reminder
                if (rsvp.Status != RsvpStatus.Accepted)
                    continue;

                try
                {
                    var prefs = await _preferencesRepository.GetByUserIdAsync(rsvp.UserId, cancellationToken).ConfigureAwait(false);

                    if (prefs == null || prefs.InAppOnGameNightInvitation)
                    {
                        var inAppNotification = new Notification(
                            id: Guid.NewGuid(),
                            userId: rsvp.UserId,
                            type: NotificationType.GameNightReminder1h,
                            severity: NotificationSeverity.Warning,
                            title: "Game Night Starting Soon!",
                            message: $"\"{notification.Title}\" starts in about 1 hour. Get ready!",
                            link: $"/game-nights/{notification.GameNightEventId}",
                            metadata: JsonSerializer.Serialize(new Dictionary<string, object>(StringComparer.Ordinal)
                            {
                                ["gameNightEventId"] = notification.GameNightEventId,
                                ["scheduledAt"] = notification.ScheduledAt
                            }),
                            correlationId: correlationId);

                        await _notificationRepository.AddAsync(inAppNotification, cancellationToken).ConfigureAwait(false);
                        notifiedCount++;
                    }
                }
#pragma warning disable CA1031
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to send 1h reminder to user {UserId} for event {EventId}",
                        rsvp.UserId, notification.GameNightEventId);
                }
#pragma warning restore CA1031
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "1h reminder notifications sent to {Count} accepted users for event {EventId} (correlationId: {CorrelationId})",
                notifiedCount, notification.GameNightEventId, correlationId);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to handle GameNightReminder1hEvent for event {EventId}",
                notification.GameNightEventId);
        }
#pragma warning restore CA1031
    }
}

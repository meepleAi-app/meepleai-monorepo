using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
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
/// Handles GameNightRsvpReceivedEvent to notify the organizer of a new RSVP response.
/// In-app notification only (no email for RSVP notifications).
/// Issue #44/#47: Game night notification types.
/// </summary>
internal sealed class GameNightRsvpReceivedNotificationHandler : INotificationHandler<GameNightRsvpReceivedEvent>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IUserRepository _userRepository;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GameNightRsvpReceivedNotificationHandler> _logger;

    public GameNightRsvpReceivedNotificationHandler(
        INotificationRepository notificationRepository,
        IUserRepository userRepository,
        MeepleAiDbContext dbContext,
        ILogger<GameNightRsvpReceivedNotificationHandler> logger)
    {
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(GameNightRsvpReceivedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            // Resolve the responding user's name
            var respondingUser = await _userRepository.GetByIdAsync(notification.UserId, cancellationToken).ConfigureAwait(false);
            var respondingUserName = respondingUser?.DisplayName ?? "A user";

            var statusText = notification.RsvpStatus switch
            {
                GameManagement.Domain.Enums.RsvpStatus.Accepted => "accepted",
                GameManagement.Domain.Enums.RsvpStatus.Declined => "declined",
                GameManagement.Domain.Enums.RsvpStatus.Maybe => "responded maybe to",
                _ => "responded to"
            };

            // In-app notification to the organizer only
            var organizerNotification = new Notification(
                id: Guid.NewGuid(),
                userId: notification.OrganizerId,
                type: NotificationType.GameNightRsvpReceived,
                severity: NotificationSeverity.Info,
                title: "RSVP Received",
                message: $"{respondingUserName} {statusText} your game night invitation",
                link: $"/game-nights/{notification.GameNightEventId}",
                metadata: JsonSerializer.Serialize(new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["gameNightEventId"] = notification.GameNightEventId,
                    ["respondingUserId"] = notification.UserId,
                    ["rsvpStatus"] = notification.RsvpStatus.ToString()
                }),
                correlationId: Guid.NewGuid());

            await _notificationRepository.AddAsync(organizerNotification, cancellationToken).ConfigureAwait(false);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "RSVP received notification created for organizer {OrganizerId} — user {UserId} {Status} event {EventId}",
                notification.OrganizerId, notification.UserId, statusText, notification.GameNightEventId);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to handle GameNightRsvpReceivedEvent for event {EventId}, user {UserId}",
                notification.GameNightEventId, notification.UserId);
        }
#pragma warning restore CA1031
    }
}

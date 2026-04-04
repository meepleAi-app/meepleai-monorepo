using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;

namespace Api.BoundedContexts.UserNotifications.Application.Services;

internal record NotificationMessage
{
    public required NotificationType Type { get; init; }
    public required Guid RecipientUserId { get; init; }
    public required INotificationPayload Payload { get; init; }
    public string? DeepLinkPath { get; init; }
    /// <summary>
    /// Optional metadata object serialized to JSON and stored on the notification.
    /// Used to carry subtype context (e.g. hours_before for game_night_reminder).
    /// </summary>
    public object? Metadata { get; init; }
}

internal interface INotificationDispatcher
{
    Task DispatchAsync(NotificationMessage message, CancellationToken ct = default);
}

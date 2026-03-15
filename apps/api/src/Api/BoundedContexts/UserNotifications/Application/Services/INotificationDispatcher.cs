using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;

namespace Api.BoundedContexts.UserNotifications.Application.Services;

internal record NotificationMessage
{
    public required NotificationType Type { get; init; }
    public required Guid RecipientUserId { get; init; }
    public required INotificationPayload Payload { get; init; }
    public string? DeepLinkPath { get; init; }
}

internal interface INotificationDispatcher
{
    Task DispatchAsync(NotificationMessage message, CancellationToken ct = default);
}

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
    /// <summary>
    /// Optional source domain event id used to dedupe at the dispatcher level (issue #1937 / CF-1).
    /// When set, the dispatcher short-circuits if a Notification with the same SourceEventId already
    /// exists for the recipient. Callers that fan out from a domain event handler MUST set this to
    /// <c>notification.EventId</c> so that re-dispatch (rollback-after-publish in #1535, retry on
    /// transient MediatR errors, browser refresh during command) does not produce duplicate notifications,
    /// emails, or Slack pings. When <c>null</c>, dispatcher falls back to legacy behavior (one row per
    /// call, no dedup) — used by hand-triggered admin notifications without an originating event.
    /// </summary>
    public Guid? SourceEventId { get; init; }
}

internal interface INotificationDispatcher
{
    Task DispatchAsync(NotificationMessage message, CancellationToken ct = default);
}

namespace Api.BoundedContexts.UserNotifications.Application.DTOs;

/// <summary>
/// DTO representing a notification queue item for admin monitoring.
/// </summary>
internal record NotificationQueueItemDto(
    Guid Id,
    string ChannelType,
    string NotificationType,
    string Status,
    int RetryCount,
    string? LastError,
    DateTime CreatedAt,
    DateTime? ProcessedAt);

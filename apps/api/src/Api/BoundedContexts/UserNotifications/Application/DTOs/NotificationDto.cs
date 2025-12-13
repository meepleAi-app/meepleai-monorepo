namespace Api.BoundedContexts.UserNotifications.Application.DTOs;

/// <summary>
/// Data transfer object for user notification.
/// Flat structure for API responses.
/// </summary>
public record NotificationDto(
    Guid Id,
    Guid UserId,
    string Type,
    string Severity,
    string Title,
    string Message,
    string? Link,
    string? Metadata,
    bool IsRead,
    DateTime CreatedAt,
    DateTime? ReadAt
);

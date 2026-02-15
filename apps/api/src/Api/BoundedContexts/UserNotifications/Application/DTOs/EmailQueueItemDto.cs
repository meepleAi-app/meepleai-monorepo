namespace Api.BoundedContexts.UserNotifications.Application.DTOs;

/// <summary>
/// Data transfer object for email queue item history.
/// Issue #4417: Email notification queue.
/// </summary>
internal record EmailQueueItemDto(
    Guid Id,
    Guid UserId,
    string To,
    string Subject,
    string Status,
    int RetryCount,
    int MaxRetries,
    string? ErrorMessage,
    DateTime CreatedAt,
    DateTime? ProcessedAt,
    DateTime? FailedAt
);

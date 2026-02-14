namespace Api.BoundedContexts.UserNotifications.Application.DTOs;

/// <summary>
/// User notification preferences response.
/// Issue #4220: Multi-channel notification configuration.
/// </summary>
internal record NotificationPreferencesDto(
    Guid UserId,
    bool EmailOnDocumentReady,
    bool EmailOnDocumentFailed,
    bool EmailOnRetryAvailable,
    bool PushOnDocumentReady,
    bool PushOnDocumentFailed,
    bool PushOnRetryAvailable,
    bool InAppOnDocumentReady,
    bool InAppOnDocumentFailed,
    bool InAppOnRetryAvailable
);

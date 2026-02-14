using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Command to update user notification preferences.
/// Issue #4220: Multi-channel notification configuration.
/// </summary>
internal record UpdateNotificationPreferencesCommand(
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
) : ICommand;

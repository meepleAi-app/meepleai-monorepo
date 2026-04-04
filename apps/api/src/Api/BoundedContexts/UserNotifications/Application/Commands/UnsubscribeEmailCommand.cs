using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Command to unsubscribe a user from a specific notification type via email link.
/// GDPR-compliant one-click unsubscribe.
/// Issue #38: Unsubscribe link in emails.
/// </summary>
internal record UnsubscribeEmailCommand(
    Guid UserId,
    string NotificationType,
    string Source = "email_unsubscribe"
) : ICommand<bool>;

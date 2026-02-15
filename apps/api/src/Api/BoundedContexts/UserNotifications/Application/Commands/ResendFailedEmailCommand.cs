using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Command to resend a failed or dead-letter email.
/// Resets the email status to pending for reprocessing.
/// Issue #4417: Email notification queue.
/// </summary>
internal record ResendFailedEmailCommand(
    Guid EmailId,
    Guid UserId
) : ICommand<bool>;

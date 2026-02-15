using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Admin command to force-retry a dead-lettered email.
/// Resets status to pending without user authorization check.
/// Issue #4430: Email queue dashboard monitoring.
/// </summary>
internal record AdminRetryEmailCommand(
    Guid EmailId
) : ICommand<bool>;

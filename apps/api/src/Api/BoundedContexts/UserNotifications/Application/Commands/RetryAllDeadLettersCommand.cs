using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Command to retry all dead-lettered emails.
/// Issue #39: Admin email management.
/// </summary>
internal record RetryAllDeadLettersCommand() : ICommand<int>;

using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Command to send a test email immediately (not queued).
/// Issue #39: Admin email management.
/// </summary>
internal record SendTestEmailCommand(string To) : ICommand<bool>;

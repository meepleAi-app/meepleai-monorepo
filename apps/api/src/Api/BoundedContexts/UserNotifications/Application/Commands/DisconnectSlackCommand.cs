using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Command to disconnect the user's Slack integration.
/// Revokes the token (best-effort) and marks the connection as disconnected.
/// </summary>
internal record DisconnectSlackCommand(Guid UserId) : ICommand<bool>;

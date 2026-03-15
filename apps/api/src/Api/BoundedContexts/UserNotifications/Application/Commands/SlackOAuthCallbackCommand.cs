using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Command to handle the Slack OAuth callback.
/// Exchanges the authorization code for an access token and creates a SlackConnection.
/// </summary>
internal record SlackOAuthCallbackCommand(string Code, string State) : ICommand<bool>;

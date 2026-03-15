using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Command to initiate Slack OAuth connection.
/// Returns the OAuth redirect URL for the user to authorize the app.
/// </summary>
internal record ConnectSlackCommand(Guid UserId) : ICommand<string>;

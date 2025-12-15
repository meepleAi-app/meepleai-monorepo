using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Command to logout a user and revoke their session.
/// </summary>
internal record LogoutCommand(
    string SessionToken
) : ICommand;

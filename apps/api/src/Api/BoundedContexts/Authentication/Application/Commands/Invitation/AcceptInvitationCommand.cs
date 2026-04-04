using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Command with Response record
namespace Api.BoundedContexts.Authentication.Application.Commands.Invitation;

/// <summary>
/// Command to accept an invitation and create a user account.
/// Issue #124: User invitation system.
/// </summary>
internal record AcceptInvitationCommand(
    string Token,
    string Password,
    string ConfirmPassword
) : ICommand<AcceptInvitationResponse>;

/// <summary>
/// Response for accepting an invitation.
/// Contains the created user, session token for immediate login, and expiry.
/// </summary>
internal sealed record AcceptInvitationResponse(
    UserDto User,
    string SessionToken,
    DateTime ExpiresAt);

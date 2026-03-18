using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands.Invitation;

/// <summary>
/// Command to activate a pending invited user account by setting a password.
/// Called when the invitee clicks the email link and submits their chosen password.
/// </summary>
internal sealed record ActivateInvitedAccountCommand(
    string Token,
    string Password
) : ICommand<ActivationResultDto>;

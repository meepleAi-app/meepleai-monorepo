using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Command with Result record
namespace Api.BoundedContexts.Authentication.Application.Commands.Invitation;

/// <summary>
/// Command to send an invitation email to a new user.
/// Issue #124: User invitation system.
/// </summary>
internal record SendInvitationCommand(
    string Email,
    string Role,
    Guid InvitedByUserId
) : ICommand<InvitationDto>;

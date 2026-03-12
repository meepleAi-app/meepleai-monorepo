using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands.Invitation;

/// <summary>
/// Command to resend an invitation by expiring the old one and creating a new one.
/// Issue #124: User invitation system.
/// </summary>
internal record ResendInvitationCommand(
    Guid InvitationId,
    Guid ResendByUserId
) : ICommand<InvitationDto>;

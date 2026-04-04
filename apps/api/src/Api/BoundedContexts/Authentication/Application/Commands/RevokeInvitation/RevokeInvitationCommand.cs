using MediatR;

namespace Api.BoundedContexts.Authentication.Application.Commands.RevokeInvitation;

/// <summary>
/// Command to revoke a pending invitation, making it immediately invalid.
/// </summary>
/// <param name="InvitationId">Invitation to revoke</param>
/// <param name="AdminUserId">Admin user requesting revocation</param>
internal sealed record RevokeInvitationCommand(
    Guid InvitationId,
    Guid AdminUserId
) : IRequest<bool>;

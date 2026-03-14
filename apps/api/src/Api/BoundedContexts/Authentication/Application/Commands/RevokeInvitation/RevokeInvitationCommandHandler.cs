using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.Authentication.Application.Commands.RevokeInvitation;

/// <summary>
/// Handler for revoking pending invitation tokens.
/// Validates the invitation exists and is in Pending status before revoking.
/// </summary>
internal sealed class RevokeInvitationCommandHandler : IRequestHandler<RevokeInvitationCommand, bool>
{
    private readonly IInvitationTokenRepository _invitationTokenRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RevokeInvitationCommandHandler(
        IInvitationTokenRepository invitationTokenRepository,
        IUnitOfWork unitOfWork)
    {
        _invitationTokenRepository = invitationTokenRepository ?? throw new ArgumentNullException(nameof(invitationTokenRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<bool> Handle(
        RevokeInvitationCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var invitation = await _invitationTokenRepository.GetByIdAsync(request.InvitationId, cancellationToken).ConfigureAwait(false);

        if (invitation == null)
        {
            return false;
        }

        if (invitation.Status != InvitationStatus.Pending)
        {
            return false;
        }

        // Revoke the invitation (domain logic)
        invitation.Revoke();

        // Save changes via repository
        await _invitationTokenRepository.UpdateAsync(invitation, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }
}

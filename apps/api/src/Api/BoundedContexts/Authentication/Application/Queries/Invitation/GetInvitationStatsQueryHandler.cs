using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries.Invitation;

/// <summary>
/// Handles retrieval of invitation statistics.
/// Issue #124: User invitation system.
/// </summary>
internal sealed class GetInvitationStatsQueryHandler
    : IQueryHandler<GetInvitationStatsQuery, InvitationStatsResponse>
{
    private readonly IInvitationTokenRepository _invitationRepo;

    public GetInvitationStatsQueryHandler(IInvitationTokenRepository invitationRepo)
    {
        _invitationRepo = invitationRepo ?? throw new ArgumentNullException(nameof(invitationRepo));
    }

    public async Task<InvitationStatsResponse> Handle(GetInvitationStatsQuery query, CancellationToken cancellationToken)
    {
        var pending = await _invitationRepo.CountByStatusAsync(InvitationStatus.Pending, cancellationToken).ConfigureAwait(false);
        var accepted = await _invitationRepo.CountByStatusAsync(InvitationStatus.Accepted, cancellationToken).ConfigureAwait(false);
        var expired = await _invitationRepo.CountByStatusAsync(InvitationStatus.Expired, cancellationToken).ConfigureAwait(false);

        return new InvitationStatsResponse(pending, accepted, expired, pending + accepted + expired);
    }
}

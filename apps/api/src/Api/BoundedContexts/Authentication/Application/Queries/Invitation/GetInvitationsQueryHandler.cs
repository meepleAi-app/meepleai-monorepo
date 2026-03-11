using Api.BoundedContexts.Authentication.Application.Commands.Invitation;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries.Invitation;

/// <summary>
/// Handles retrieval of paginated invitation list.
/// Issue #124: User invitation system.
/// </summary>
internal sealed class GetInvitationsQueryHandler
    : IQueryHandler<GetInvitationsQuery, GetInvitationsResponse>
{
    private readonly IInvitationTokenRepository _invitationRepo;

    public GetInvitationsQueryHandler(IInvitationTokenRepository invitationRepo)
    {
        _invitationRepo = invitationRepo ?? throw new ArgumentNullException(nameof(invitationRepo));
    }

    public async Task<GetInvitationsResponse> Handle(GetInvitationsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        InvitationStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<InvitationStatus>(query.Status, true, out var parsed))
            statusFilter = parsed;

        var invitations = await _invitationRepo.GetByStatusAsync(
            statusFilter, query.Page, query.PageSize, cancellationToken).ConfigureAwait(false);

        // Count total for the filter
        int totalCount;
        if (statusFilter.HasValue)
        {
            totalCount = await _invitationRepo.CountByStatusAsync(statusFilter.Value, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Count all statuses
            var pending = await _invitationRepo.CountByStatusAsync(InvitationStatus.Pending, cancellationToken).ConfigureAwait(false);
            var accepted = await _invitationRepo.CountByStatusAsync(InvitationStatus.Accepted, cancellationToken).ConfigureAwait(false);
            var expired = await _invitationRepo.CountByStatusAsync(InvitationStatus.Expired, cancellationToken).ConfigureAwait(false);
            totalCount = pending + accepted + expired;
        }

        var items = invitations.Select(SendInvitationCommandHandler.MapToDto).ToList();

        return new GetInvitationsResponse(items, totalCount, query.Page, query.PageSize);
    }
}

using Api.BoundedContexts.Authentication.Application.Commands.Invitation;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries.Invitation;

/// <summary>
/// Query to get pending invitations, optionally filtered by status.
/// Used by admin panel to view invitation queue.
/// Issue #124: User invitation system.
/// </summary>
internal sealed record GetPendingInvitationsQuery(string? StatusFilter) : IQuery<List<InvitationDto>>;

/// <summary>
/// Handles retrieval of invitations filtered by status.
/// Returns all invitations when no filter is provided.
/// Issue #124: User invitation system.
/// </summary>
internal sealed class GetPendingInvitationsQueryHandler
    : IQueryHandler<GetPendingInvitationsQuery, List<InvitationDto>>
{
    private readonly IInvitationTokenRepository _invitationRepo;

    public GetPendingInvitationsQueryHandler(IInvitationTokenRepository invitationRepo)
    {
        _invitationRepo = invitationRepo ?? throw new ArgumentNullException(nameof(invitationRepo));
    }

    public async Task<List<InvitationDto>> Handle(
        GetPendingInvitationsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        InvitationStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(query.StatusFilter) &&
            Enum.TryParse<InvitationStatus>(query.StatusFilter, ignoreCase: true, out var parsed))
        {
            statusFilter = parsed;
        }

        // Use a reasonable default page size; callers who need pagination should use GetInvitationsQuery instead
        var invitations = await _invitationRepo.GetByStatusAsync(
            statusFilter, page: 1, pageSize: 500, cancellationToken).ConfigureAwait(false);

        return invitations
            .Select(inv => SendInvitationCommandHandler.MapToDto(inv))
            .ToList();
    }
}

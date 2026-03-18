using Api.BoundedContexts.Authentication.Application.Commands.Invitation;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.Middleware.Exceptions;

namespace Api.BoundedContexts.Authentication.Application.Queries.Invitation;

/// <summary>
/// Query to get a single invitation by its ID.
/// Used by admin panel to view invitation details.
/// Issue #124: User invitation system.
/// </summary>
internal sealed record GetInvitationByIdQuery(Guid Id) : IQuery<InvitationDto>;

/// <summary>
/// Handles retrieval of a single invitation by ID.
/// Throws NotFoundException when the invitation does not exist.
/// Issue #124: User invitation system.
/// </summary>
internal sealed class GetInvitationByIdQueryHandler
    : IQueryHandler<GetInvitationByIdQuery, InvitationDto>
{
    private readonly IInvitationTokenRepository _invitationRepo;

    public GetInvitationByIdQueryHandler(IInvitationTokenRepository invitationRepo)
    {
        _invitationRepo = invitationRepo ?? throw new ArgumentNullException(nameof(invitationRepo));
    }

    public async Task<InvitationDto> Handle(
        GetInvitationByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var invitation = await _invitationRepo.GetByIdAsync(query.Id, cancellationToken).ConfigureAwait(false);

        if (invitation == null)
            throw new NotFoundException($"Invitation with ID {query.Id} not found");

        return SendInvitationCommandHandler.MapToDto(invitation);
    }
}

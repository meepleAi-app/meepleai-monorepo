using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Lists the guests invited to a game night by email, for the organizer.
/// #4706: they live in <c>game_night_invitations</c>, apart from the member RSVPs the other reads cover.
/// </summary>
internal sealed class GetGameNightGuestInvitationsQueryHandler
    : IQueryHandler<GetGameNightGuestInvitationsQuery, IReadOnlyList<GameNightGuestInvitationDto>>
{
    private readonly IGameNightEventRepository _repository;
    private readonly MeepleAiDbContext _context;

    public GetGameNightGuestInvitationsQueryHandler(
        IGameNightEventRepository repository,
        MeepleAiDbContext context)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<GameNightGuestInvitationDto>> Handle(
        GetGameNightGuestInvitationsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var gameNight = await _repository.GetByIdAsync(query.GameNightId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameNightEvent", query.GameNightId.ToString());

        // Organizer only: an invited member may read the night and its roster, but the guests' email
        // addresses are the organizer's data about people who are not members.
        if (gameNight.OrganizerId != query.CallerUserId)
            throw new ForbiddenException("Only the organizer can view the guests invited by email.");

        var cancelled = nameof(GameNightInvitationStatus.Cancelled);
        var rows = await _context.GameNightInvitations
            .AsNoTracking()
            .Where(i => i.GameNightId == query.GameNightId && i.Status != cancelled)
            .OrderBy(i => i.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(i => new GameNightGuestInvitationDto(
            Id: i.Id,
            Email: i.Email,
            Status: i.Status,
            RespondedAt: i.RespondedAt,
            RespondedByName: i.RespondedByName,
            ExpiresAt: i.ExpiresAt,
            CreatedAt: i.CreatedAt)).ToList();
    }
}

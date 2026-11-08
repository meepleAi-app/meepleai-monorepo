using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.BoundedContexts.GameManagement.Application.Queries.GameNights;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Handles retrieval of a game night by ID.
/// Issue #46: GameNight API endpoints.
/// Issue #43: Enriches DTO with OrganizerName, uses NotFoundException.
/// </summary>
internal sealed class GetGameNightByIdQueryHandler : IQueryHandler<GetGameNightByIdQuery, GameNightDto>
{
    private readonly IGameNightEventRepository _repository;
    private readonly MeepleAiDbContext _context;

    public GetGameNightByIdQueryHandler(
        IGameNightEventRepository repository,
        MeepleAiDbContext context)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<GameNightDto> Handle(GetGameNightByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var gameNight = await _repository.GetByIdAsync(query.GameNightId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameNightEvent", query.GameNightId.ToString());

        // #2698: participant-scoped read (organizer or invited) — closes the cross-tenant IDOR
        // where any authenticated user could read another user's private game night. The rule is
        // written once, in GameNightParticipation (#4759: it also covers the email guest).
        var isParticipant = await GameNightParticipation
            .IsParticipantAsync(gameNight, query.CallerUserId, _repository, cancellationToken)
            .ConfigureAwait(false);
        if (!isParticipant)
            throw new ForbiddenException("Only the organizer or an invited player can view this game night.");

        var organizerName = await GetUserDisplayNameAsync(gameNight.OrganizerId, cancellationToken).ConfigureAwait(false);

        // #4706: guests invited by email are not in the RSVP rows; add them to the counts.
        var guests = await GuestInvitationCounts.LoadAsync(_context, [gameNight.Id], cancellationToken).ConfigureAwait(false);

        // #4921: the viewer's own state, derived for the email guest too (no RSVP row on that path): without
        // it the client showed «not invited / no answer» to someone who had accepted.
        var viewerRsvpStatus = await GameNightParticipation
            .ViewerRsvpStatusAsync(gameNight, query.CallerUserId, _repository, cancellationToken)
            .ConfigureAwait(false);

        return GameNightMapperHelper.MapToDto(
            gameNight, organizerName, query.CallerUserId,
            guests.GetValueOrDefault(gameNight.Id), viewerRsvpStatus);
    }

    private async Task<string> GetUserDisplayNameAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            .ConfigureAwait(false);

        return user?.DisplayName ?? user?.Email ?? "Unknown";
    }
}

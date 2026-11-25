using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.BoundedContexts.GameManagement.Application.Queries.GameNights;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Api.SharedKernel.Domain.Validation;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Handles retrieval of RSVPs for a game night.
/// Issue #46: GameNight API endpoints.
/// Issue #43: Enriches DTO with UserName via batch user lookup, uses NotFoundException.
/// </summary>
internal sealed class GetGameNightRsvpsQueryHandler : IQueryHandler<GetGameNightRsvpsQuery, IReadOnlyList<GameNightRsvpDto>>
{
    private readonly IGameNightEventRepository _repository;
    private readonly MeepleAiDbContext _context;

    public GetGameNightRsvpsQueryHandler(
        IGameNightEventRepository repository,
        MeepleAiDbContext context)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<GameNightRsvpDto>> Handle(GetGameNightRsvpsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var gameNight = await _repository.GetByIdAsync(query.GameNightId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameNightEvent", query.GameNightId.ToString());

        // #2698: participant-scoped read (organizer or invited) — closes the cross-tenant IDOR
        // where any authenticated user could read another user's roster + member names.
        var isParticipant = await GameNightParticipation
            .IsParticipantAsync(gameNight, query.CallerUserId, _repository, cancellationToken)
            .ConfigureAwait(false);
        if (!isParticipant)
            throw new ForbiddenException("Only the organizer or an invited player can view this game night's RSVPs.");

        var userIds = gameNight.Rsvps.Select(r => r.UserId).Distinct().ToList();
        var userNames = await GameNightUserNames.LoadAsync(_context, userIds, PlayerDisplayNames.Player, cancellationToken).ConfigureAwait(false);

        return gameNight.Rsvps.Select(r => GameNightMapperHelper.MapToRsvpDto(
            r, userNames.GetValueOrDefault(r.UserId, "Unknown"))).ToList();
    }
}

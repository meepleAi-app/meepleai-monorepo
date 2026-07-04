using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Handles <see cref="GetGameNightLiveQuery"/>. Loads the <c>GameNightEvent</c> (its child
/// <c>Sessions</c> are already Included by the repository) and projects it to the live read model,
/// ordering sessions by play order. Also surfaces the un-started planned line-up (WS1 DEC-9).
/// </summary>
internal sealed class GetGameNightLiveQueryHandler : IQueryHandler<GetGameNightLiveQuery, GameNightLiveDto>
{
    private readonly IGameNightEventRepository _repository;
    private readonly MeepleAiDbContext _db;

    public GetGameNightLiveQueryHandler(IGameNightEventRepository repository, MeepleAiDbContext db)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<GameNightLiveDto> Handle(GetGameNightLiveQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var gameNight = await _repository.GetByIdAsync(query.GameNightId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameNightEvent", query.GameNightId.ToString());

        // Participant-only: the live read exposes per-session WinnerId (user GUIDs) + progression.
        var isOrganizer = gameNight.OrganizerId == query.CallerUserId;
        var isParticipant = isOrganizer
            || gameNight.Rsvps.Any(r => r.UserId == query.CallerUserId);
        if (!isParticipant)
            throw new ForbiddenException("Only the organizer or an invited player can view the night-live state.");

        var sessions = gameNight.Sessions
            .OrderBy(s => s.PlayOrder)
            .Select(s => new GameNightSessionDto(
                s.SessionId,
                s.GameId,
                s.GameTitle,
                s.PlayOrder,
                s.Status,
                s.WinnerId,
                s.StartedAt,
                s.CompletedAt))
            .ToList();

        // WS1 DEC-9: the planned games (GameIds) not yet started as a Session, in planned order.
        var startedGameIds = gameNight.Sessions.Select(s => s.GameId).ToHashSet();
        var unstartedGameIds = gameNight.GameIds.Distinct().Where(id => !startedGameIds.Contains(id)).ToList();
        var titles = unstartedGameIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.SharedGames.AsNoTracking()
                .Where(g => unstartedGameIds.Contains(g.Id))
                .Select(g => new { g.Id, g.Title })
                .ToDictionaryAsync(g => g.Id, g => g.Title, cancellationToken)
                .ConfigureAwait(false);
        var plannedLineup = unstartedGameIds
            .Select(id => new GameNightLineupItemDto(id, titles.TryGetValue(id, out var t) ? t : "Gioco"))
            .ToList();

        return new GameNightLiveDto(
            gameNight.Id, gameNight.Title, gameNight.Status, sessions, isOrganizer, plannedLineup);
    }
}

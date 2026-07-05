using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
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

        // #2634 C4: batch-load the tracking-Session participants for winner-name resolution + the
        // in-progress roster (the winner picker). Deliberate GameManagement→SessionTracking cross-BC
        // read over the participant table; scoped to this night's sessions only.
        var sessionIds = gameNight.Sessions.Select(s => s.SessionId).ToList();
        var participants = sessionIds.Count == 0
            ? new List<ParticipantRef>()
            : await _db.SessionTrackingParticipants.AsNoTracking()
                .Where(p => sessionIds.Contains(p.SessionId))
                .Select(p => new ParticipantRef(p.Id, p.SessionId, p.DisplayName))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        // Scoped by (SessionId, WinnerId): a stray/foreign Participant.Id fails closed to null
        // rather than resolving a plausible-but-wrong name (panel D2).
        string? WinnerNameFor(GameNightSession s) =>
            s.WinnerId is { } wid
                ? participants
                    .Where(p => p.SessionId == s.SessionId && p.Id == wid)
                    .Select(p => p.DisplayName)
                    .FirstOrDefault()
                : null;

        var sessions = gameNight.Sessions
            .OrderBy(s => s.PlayOrder)
            .Select(s => new GameNightSessionDto(
                s.SessionId,
                s.GameId,
                s.GameTitle,
                s.PlayOrder,
                s.Status,
                s.WinnerId,
                WinnerNameFor(s),
                s.StartedAt,
                s.CompletedAt))
            .ToList();

        // The winner picker candidates = the participants of the (single) in-progress session.
        var inProgressSessionId = gameNight.Sessions
            .FirstOrDefault(s => s.Status == GameNightSessionStatus.InProgress)?.SessionId;
        var currentSessionRoster = inProgressSessionId is { } liveId
            ? participants
                .Where(p => p.SessionId == liveId)
                .Select(p => new GameNightRosterMemberDto(p.Id, p.DisplayName))
                .ToList()
            : new List<GameNightRosterMemberDto>();

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
            gameNight.Id, gameNight.Title, gameNight.Status, sessions, isOrganizer, plannedLineup,
            currentSessionRoster);
    }

    /// <summary>A tracking-Session participant, flattened for the cross-BC roster read.</summary>
    private sealed record ParticipantRef(Guid Id, Guid SessionId, string DisplayName);
}

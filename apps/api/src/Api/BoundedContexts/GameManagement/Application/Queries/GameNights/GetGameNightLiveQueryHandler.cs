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

        // #3188 Slice 6 (decision D4): the canonical liveness signal is the tracking Session's live
        // state (Session.IsLive = started_at != null && finalized_at == null), owned by
        // SessionTracking — NOT the raw game_night_sessions link Status. Batch-load each linked
        // tracking Session's live flag (mirrors the participants read above: one query, AsNoTracking,
        // scoped to this night's sessionIds). A session with no tracking row — or a not-yet-started
        // one — is NOT live, so a split-brain link (stuck InProgress but tracking never started)
        // fails closed to not-live.
        var liveStates = sessionIds.Count == 0
            ? new Dictionary<Guid, bool>()
            : await _db.SessionTrackingSessions.AsNoTracking()
                .Where(ts => sessionIds.Contains(ts.Id))
                .Select(ts => new { ts.Id, IsLive = ts.StartedAt != null && ts.FinalizedAt == null })
                .ToDictionaryAsync(x => x.Id, x => x.IsLive, cancellationToken)
                .ConfigureAwait(false);

        bool IsLiveFor(GameNightSession s) =>
            liveStates.TryGetValue(s.SessionId, out var live) && live;

        var sessions = gameNight.Sessions
            .OrderBy(s => s.PlayOrder)
            .Select(s => new GameNightSessionDto(
                s.SessionId,
                s.GameId,
                s.GameTitle,
                s.PlayOrder,
                s.Status,
                IsLiveFor(s),
                s.WinnerId,
                WinnerNameFor(s),
                s.StartedAt,
                s.CompletedAt))
            .ToList();

        // The winner picker candidates = the participants of the (single) live session, where "live"
        // is the canonical Session.IsLive (D4) rather than the raw link Status==InProgress. On the
        // go-live path both agree; using IsLive unifies the two read paths and wins any split-brain.
        // Lowest PlayOrder disambiguates a racy >1-live read deterministically.
        var inProgressSessionId = gameNight.Sessions
            .Where(IsLiveFor)
            .OrderBy(s => s.PlayOrder)
            .FirstOrDefault()?.SessionId;
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

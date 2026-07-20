using Api.BoundedContexts.GameManagement.Domain.Enums;

namespace Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;

/// <summary>
/// Read model for the night-live view (SI-2 #2633 prerequisite, spec
/// 2026-07-01-game-night-live-wire-design). The list <see cref="GameNightDto"/> stays lean;
/// this is the distinct live read concern — the night header + its session progression, which
/// the FE <c>useGameNightLive(id)</c> hook maps onto the (currently fixture) NightLiveHub.
/// </summary>
public sealed record GameNightLiveDto(
    Guid Id,
    string Title,
    GameNightStatus Status,
    IReadOnlyList<GameNightSessionDto> Sessions,
    // WS1 DEC-9: true when the caller is the night's organizer — gates the FE
    // organizer-only "Avvia prossimo gioco" CTA. The read admits organizer + invited
    // participants, so a viewer flag is needed to distinguish them.
    bool IsViewerOrganizer,
    // WS1 DEC-9: the planned games not yet turned into a Session, in planned order.
    // The CTA starts the first of these (needs GameId + GameTitle for POST /sessions).
    IReadOnlyList<GameNightLineupItemDto> PlannedLineup,
    // #2634 C4: the in-progress session's participants (Participant.Id + DisplayName), sourced from
    // this already-participant-guarded read so the winner picker never hits the unguarded
    // GET /game-sessions/{id} roster (IDOR). Empty when no game is live.
    IReadOnlyList<GameNightRosterMemberDto> CurrentSessionRoster);

/// <summary>
/// A planned game in the night's line-up that has not yet been started as a Session.
/// </summary>
public sealed record GameNightLineupItemDto(Guid GameId, string GameTitle);

/// <summary>
/// #2634 C4: a candidate winner in the current live session — a tracking-Session Participant.
/// <see cref="ParticipantId"/> is what the FE sends as the winner on POST /sessions/complete.
/// </summary>
public sealed record GameNightRosterMemberDto(Guid ParticipantId, string DisplayName);

/// <summary>
/// A single sitting within the night — a projection of the <c>GameNightSession</c> child entity.
/// Score and estimated-time (fixture-only in the mockup) are intentionally omitted: they are not
/// in the domain (spec decision D-SCORE/TIME). Elapsed/duration is derivable from
/// <see cref="StartedAt"/>/<see cref="CompletedAt"/> on the FE.
/// </summary>
public sealed record GameNightSessionDto(
    Guid SessionId,
    Guid GameId,
    string GameTitle,
    int PlayOrder,
    GameNightSessionStatus Status,
    // #3188 Slice 6 (decision D4): the canonical liveness signal — sourced from the tracking
    // Session's live state (Session.IsLive = started_at != null && finalized_at == null), owned by
    // SessionTracking. ADDITIVE to <see cref="Status"/>, which stays the derived link projection:
    // on the canonical go-live path both agree, but IsLive unifies the two read paths and wins any
    // residual split-brain (a link stuck at InProgress whose tracking Session never went live).
    bool IsLive,
    Guid? WinnerId,
    // #2634 C4: the WinnerId (a tracking-Session Participant.Id) resolved to a display name,
    // scoped by (SessionId, WinnerId) so a stray/foreign id fails closed to null. null when the
    // session has no winner (or it could not be resolved).
    string? WinnerName,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt);

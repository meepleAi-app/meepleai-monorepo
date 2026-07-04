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
    bool IsViewerOrganizer);

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
    Guid? WinnerId,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt);

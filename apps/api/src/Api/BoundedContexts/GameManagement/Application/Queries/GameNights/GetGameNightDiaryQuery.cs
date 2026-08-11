using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Query to get the cross-game diary timeline for a game night.
/// Returns all session events tagged with the game night ID, ordered chronologically.
/// Participant-scoped (#2633 C2): only the organizer or an RSVP'd player may read it — parity
/// with <c>GetGameNightLiveQuery</c>, which the live hub already enforces.
/// </summary>
internal record GetGameNightDiaryQuery(Guid GameNightId, Guid CallerUserId) : IQuery<GameNightDiaryDto>;

internal record GameNightDiaryDto(
    Guid GameNightId,
    List<GameNightDiaryEntryDto> Entries);

internal record GameNightDiaryEntryDto(
    Guid Id,
    Guid SessionId,
    string EventType,
    string Description,
    string? Payload,
    Guid? ActorId,
    DateTime Timestamp);

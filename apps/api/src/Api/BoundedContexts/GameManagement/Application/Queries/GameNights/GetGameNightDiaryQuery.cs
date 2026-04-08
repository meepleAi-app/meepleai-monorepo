using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Query to get the cross-game diary timeline for a game night.
/// Returns all session events tagged with the game night ID, ordered chronologically.
/// </summary>
internal record GetGameNightDiaryQuery(Guid GameNightId) : IQuery<GameNightDiaryDto>;

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

using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.GameKbStatus;

/// <summary>
/// Query to retrieve the KB status for all games that have at least one
/// processed PDF, plus games with no KB yet (joined from Games table).
/// Used by the Admin KB Games overview dashboard.
/// </summary>
internal sealed record GetGameKbStatusesQuery : IRequest<GetGameKbStatusesResult>;

/// <summary>
/// Aggregated result with one entry per game.
/// </summary>
internal sealed record GetGameKbStatusesResult(List<GameKbStatusDto> Items);

/// <summary>
/// KB status DTO for a single game.
/// </summary>
internal sealed record GameKbStatusDto(
    Guid GameId,
    string GameName,
    string KbStatus,           // "complete" | "partial" | "none"
    int DocumentCount,
    int TotalChunks,
    DateTime? LatestIndexedAt,
    bool HasAutoBackup);

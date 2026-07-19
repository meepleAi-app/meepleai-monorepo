

#pragma warning disable MA0048 // File name must match type name - Contains related Request/Response DTOs
namespace Api.BoundedContexts.GameManagement.Application.DTOs;

/// <summary>
/// Data transfer object for game session information.
/// </summary>
internal record GameSessionDto(
    Guid Id,
    Guid GameId,
    string Status,
    DateTime StartedAt,
    DateTime? CompletedAt,
    int PlayerCount,
    IReadOnlyList<SessionPlayerDto> Players,
    string? WinnerName,
    string? Notes,
    int DurationMinutes,
    // #3080: polymorphic score surfaced in session history. Null when the session
    // has no correlated live/tracking session (e.g. legacy or non-live sessions).
    // ScoringType ∈ { "Points", "BinaryWin", "Objectives", "Ranking" };
    // ScoreData is the raw JSON payload (shape varies by ScoringType).
    string? ScoringType = null,
    string? ScoreData = null,
    // #3022: identity + score-aligned players for the summary flavor. Populated ONLY on
    // GET /api/v1/sessions/{id}; null on list/history paths (GameSessionMapper.ToDto).
    string? GameSlug = null,
    string? GameName = null,
    IReadOnlyList<ScorePlayerDto>? ScorePlayers = null
);

/// <summary>
/// DTO for session player information.
/// </summary>
internal record SessionPlayerDto(
    string PlayerName,
    int PlayerOrder,
    string? Color
);

/// <summary>
/// #3022: a player whose Id matches scoreData.scores[].playerId (LiveGameSession player),
/// carrying the display name and color needed to render per-player scores in the summary.
/// </summary>
internal record ScorePlayerDto(
    Guid Id,
    string DisplayName,
    string? Color
);

/// <summary>
/// DTO for player in session.
/// </summary>
internal record SessionPlayerRequest(
    string PlayerName,
    int PlayerOrder,
    string? Color = null
);

/// <summary>
/// DTO for completing a session.
/// </summary>
internal record CompleteSessionRequest(
    string? WinnerName = null
);

/// <summary>
/// DTO for aggregated session statistics.
/// </summary>
internal record SessionStatsDto(
    int TotalSessions,
    int CompletedSessions,
    int AbandonedSessions,
    int AverageDurationMinutes,
    IReadOnlyList<PlayerWinStatsDto> TopPlayers
);

/// <summary>
/// DTO for player win statistics.
/// </summary>
internal record PlayerWinStatsDto(
    string PlayerName,
    int WinCount,
    decimal WinRate
);

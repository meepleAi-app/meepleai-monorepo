

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
    string? ScoreData = null
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

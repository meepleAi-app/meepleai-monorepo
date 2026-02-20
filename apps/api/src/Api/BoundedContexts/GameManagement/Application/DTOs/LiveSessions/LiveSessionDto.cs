using Api.BoundedContexts.GameManagement.Domain.Enums;

namespace Api.BoundedContexts.GameManagement.Application.DTOs.LiveSessions;

/// <summary>
/// Full DTO for live game session details.
/// Issue #4749: CQRS queries for live sessions.
/// </summary>
internal record LiveSessionDto(
    Guid Id,
    string SessionCode,
    Guid? GameId,
    string GameName,
    Guid CreatedByUserId,
    LiveSessionStatus Status,
    PlayRecordVisibility Visibility,
    Guid? GroupId,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? PausedAt,
    DateTime? CompletedAt,
    DateTime UpdatedAt,
    DateTime? LastSavedAt,
    int CurrentTurnIndex,
    Guid? CurrentTurnPlayerId,
    AgentSessionMode AgentMode,
    Guid? ChatSessionId,
    string? Notes,
    IReadOnlyList<LiveSessionPlayerDto> Players,
    IReadOnlyList<LiveSessionTeamDto> Teams,
    IReadOnlyList<LiveSessionRoundScoreDto> RoundScores,
    LiveSessionScoringConfigDto ScoringConfig
);

/// <summary>
/// Compact summary DTO for session list views.
/// </summary>
internal record LiveSessionSummaryDto(
    Guid Id,
    string SessionCode,
    string GameName,
    LiveSessionStatus Status,
    int PlayerCount,
    int CurrentTurnIndex,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? LastSavedAt
);

/// <summary>
/// DTO for a player in a live session.
/// </summary>
internal record LiveSessionPlayerDto(
    Guid Id,
    Guid? UserId,
    string DisplayName,
    string? AvatarUrl,
    PlayerColor Color,
    PlayerRole Role,
    Guid? TeamId,
    int TotalScore,
    int CurrentRank,
    DateTime JoinedAt,
    bool IsActive
);

/// <summary>
/// DTO for a team in a live session.
/// </summary>
internal record LiveSessionTeamDto(
    Guid Id,
    string Name,
    string Color,
    IReadOnlyList<Guid> PlayerIds,
    int TeamScore,
    int CurrentRank
);

/// <summary>
/// DTO for a round score entry.
/// </summary>
internal record LiveSessionRoundScoreDto(
    Guid PlayerId,
    int Round,
    string Dimension,
    int Value,
    string? Unit,
    DateTime RecordedAt
);

/// <summary>
/// DTO for scoring configuration.
/// </summary>
internal record LiveSessionScoringConfigDto(
    IReadOnlyList<string> EnabledDimensions,
    IReadOnlyDictionary<string, string> DimensionUnits
);

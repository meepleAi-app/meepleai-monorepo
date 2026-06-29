using Api.BoundedContexts.GameManagement.Domain.Enums;

namespace Api.BoundedContexts.GameManagement.Application.DTOs.LiveSessions;

/// <summary>
/// Public, code-addressable read-only lobby/scoreboard projection of a live session (#2590).
/// Deliberately narrow: NO CreatedByUserId, no player UserId, no Notes/RoundScores/Teams/GroupId/Visibility/timestamps.
/// Served by GET /api/v1/live-sessions/code/{code}/public (AllowAnonymous + rate-limited).
/// </summary>
public sealed record PublicLiveSessionDto(
    Guid Id,
    string SessionCode,
    string GameName,
    string GameSlug,
    LiveSessionStatus Status,
    IReadOnlyList<PublicLiveSessionPlayerDto> Players);

/// <summary>
/// Public scoreboard player projection — exposes the session-scoped player Id only, never the linked UserId.
/// </summary>
public sealed record PublicLiveSessionPlayerDto(
    Guid Id,
    string DisplayName,
    PlayerColor Color,
    int TotalScore,
    int CurrentRank,
    bool IsActive);

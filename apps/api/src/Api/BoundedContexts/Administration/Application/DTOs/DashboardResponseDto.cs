namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// Complete dashboard response DTO for public API endpoint (Issue #3972).
/// Aggregates user info, stats, active sessions, library snapshot, activity, and chats.
/// Schema aligned with frontend DashboardData type.
/// </summary>
public record DashboardResponseDto(
    DashboardUserDto User,
    DashboardUserStatsDto Stats,
    IReadOnlyList<DashboardSessionDto> ActiveSessions,
    DashboardLibrarySnapshotDto LibrarySnapshot,
    IReadOnlyList<DashboardActivityDto> RecentActivity,
    IReadOnlyList<DashboardChatDto> ChatHistory
);

/// <summary>
/// User info for dashboard (Issue #3972).
/// </summary>
public record DashboardUserDto(
    Guid Id,
    string Username,
    string Email
);

/// <summary>
/// Dashboard stats - flat structure matching frontend DashboardStats (Issue #3972).
/// </summary>
public record DashboardUserStatsDto(
    int LibraryCount,
    int PlayedLast30Days,
    int ChatCount,
    int WishlistCount,
    int CurrentStreak
);

/// <summary>
/// Active session for dashboard (Issue #3972).
/// </summary>
public record DashboardSessionDto(
    Guid Id,
    string GameName,
    Guid GameId,
    string? CoverUrl,
    DashboardPlayersDto Players,
    DashboardProgressDto Progress,
    DateTime LastActivity
);

/// <summary>
/// Players info for session (Issue #3972).
/// </summary>
public record DashboardPlayersDto(
    int Current,
    int Total
);

/// <summary>
/// Progress info for session (Issue #3972).
/// </summary>
public record DashboardProgressDto(
    int Turn,
    string Duration
);

/// <summary>
/// Library snapshot for dashboard (Issue #3972).
/// </summary>
public record DashboardLibrarySnapshotDto(
    DashboardQuotaDto Quota,
    IReadOnlyList<DashboardTopGameDto> TopGames
);

/// <summary>
/// Library quota info (Issue #3972).
/// </summary>
public record DashboardQuotaDto(
    int Used,
    int Total
);

/// <summary>
/// Top game for library snapshot (Issue #3972).
/// </summary>
public record DashboardTopGameDto(
    Guid Id,
    string Title,
    string? CoverUrl,
    decimal Rating,
    int PlayCount
);

/// <summary>
/// Activity event for dashboard (Issue #3972).
/// Polymorphic fields: gameId/gameName for game events, sessionId for session events,
/// chatId/topic for chat events.
/// </summary>
public record DashboardActivityDto(
    string Id,
    string Type,
    string? GameId,
    string? GameName,
    string? SessionId,
    string? ChatId,
    string? Topic,
    DateTime Timestamp
);

/// <summary>
/// Chat thread for dashboard (Issue #3972).
/// </summary>
public record DashboardChatDto(
    string Id,
    string Topic,
    int? MessageCount,
    DateTime Timestamp
);

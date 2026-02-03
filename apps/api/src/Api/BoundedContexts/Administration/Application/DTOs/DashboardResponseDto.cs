namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// Complete dashboard response DTO for public API endpoint (Issue #3314).
/// Aggregates user info, stats, active sessions, library snapshot, activity, and chats.
/// </summary>
public record DashboardResponseDto(
    DashboardUserDto User,
    DashboardStatsResponseDto Stats,
    IReadOnlyList<DashboardSessionDto> ActiveSessions,
    DashboardLibrarySnapshotDto LibrarySnapshot,
    IReadOnlyList<DashboardActivityDto> RecentActivity,
    IReadOnlyList<DashboardChatDto> RecentChats
);

/// <summary>
/// User info for dashboard (Issue #3314).
/// </summary>
public record DashboardUserDto(
    string Name,
    DateTime LastAccess
);

/// <summary>
/// Dashboard stats with trends (Issue #3314).
/// </summary>
public record DashboardStatsResponseDto(
    DashboardStatItemDto Collection,
    DashboardPlayedStatDto Played,
    DashboardStatCountDto Chats,
    DashboardStatItemDto Wishlist
);

/// <summary>
/// Stat item with total and trend (Issue #3314).
/// </summary>
public record DashboardStatItemDto(
    int Total,
    int Trend
);

/// <summary>
/// Played stat with streak (Issue #3314).
/// </summary>
public record DashboardPlayedStatDto(
    int Total,
    int Streak
);

/// <summary>
/// Stat count only (Issue #3314).
/// </summary>
public record DashboardStatCountDto(
    int Total
);

/// <summary>
/// Active session for dashboard (Issue #3314).
/// </summary>
public record DashboardSessionDto(
    Guid Id,
    string GameName,
    Guid GameId,
    DateTime StartDate,
    DashboardPlayersDto Players,
    int Turn,
    int Duration
);

/// <summary>
/// Players info for session (Issue #3314).
/// </summary>
public record DashboardPlayersDto(
    int Current,
    int Max
);

/// <summary>
/// Library snapshot for dashboard (Issue #3314).
/// </summary>
public record DashboardLibrarySnapshotDto(
    DashboardQuotaDto Quota,
    IReadOnlyList<DashboardTopGameDto> TopGames
);

/// <summary>
/// Library quota info (Issue #3314).
/// </summary>
public record DashboardQuotaDto(
    int Used,
    int Total,
    int Percentage
);

/// <summary>
/// Top game for library snapshot (Issue #3314).
/// </summary>
public record DashboardTopGameDto(
    Guid Id,
    string Title,
    string? CoverUrl,
    decimal Rating,
    int PlayCount
);

/// <summary>
/// Activity event for dashboard (Issue #3314).
/// </summary>
public record DashboardActivityDto(
    string Id,
    string Type,
    string Title,
    string? EntityId,
    DateTime Timestamp
);

/// <summary>
/// Chat thread for dashboard (Issue #3314).
/// </summary>
public record DashboardChatDto(
    string Id,
    string Title,
    DateTime LastMessageAt
);

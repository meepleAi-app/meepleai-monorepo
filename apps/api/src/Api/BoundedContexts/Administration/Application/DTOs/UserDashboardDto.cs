namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// Aggregate DTO for user dashboard with all sections (Issue #2854).
/// Combines data from multiple bounded contexts: UserLibrary, GameManagement, KnowledgeBase.
/// </summary>
internal record UserDashboardDto(
    IReadOnlyList<RecentGameDto> RecentGames,
    IReadOnlyList<ActiveSessionDto> ActiveSessions,
    IReadOnlyList<RecentChatDto> RecentChats,
    LibraryQuotaDto LibraryQuota
);

/// <summary>
/// DTO for recent games in user library.
/// </summary>
internal record RecentGameDto(
    Guid GameId,
    string Title,
    string? CoverImageUrl,
    DateTime AddedAt,
    bool IsFavorite
);

/// <summary>
/// DTO for active game sessions.
/// </summary>
internal record ActiveSessionDto(
    Guid SessionId,
    Guid GameId,
    string GameTitle,
    string Status,
    DateTime StartedAt,
    int PlayerCount
);

/// <summary>
/// DTO for recent chat history.
/// </summary>
internal record RecentChatDto(
    Guid ChatId,
    string? GameName,
    string? Title,
    string LastMessage,
    DateTime LastMessageAt
);

/// <summary>
/// DTO for user library quota information.
/// </summary>
internal record LibraryQuotaDto(
    int CurrentCount,
    int MaxAllowed,
    int Percentage
);

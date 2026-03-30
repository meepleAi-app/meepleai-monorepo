namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

/// <summary>
/// Comprehensive DTO for game detail page with full statistics and metadata.
/// </summary>
internal record GameDetailDto(
    Guid Id,
    Guid UserId,
    Guid GameId,

    // Game metadata (from SharedGameCatalog)
    string GameTitle,
    string? GamePublisher,
    int? GameYearPublished,
    string? GameDescription,
    string? GameIconUrl,
    string? GameImageUrl,
    int? MinPlayers,
    int? MaxPlayers,
    int? PlayTimeMinutes,
    decimal? ComplexityRating,
    decimal? AverageRating,

    // Library metadata
    DateTime AddedAt,
    string? Notes,
    bool IsFavorite,

    // Game state
    string CurrentState, // "Nuovo", "InPrestito", "Wishlist", "Owned"
    DateTime? StateChangedAt,
    string? StateNotes,
    bool IsAvailableForPlay,

    // Game statistics
    int TimesPlayed,
    DateTime? LastPlayed,
    string? WinRate, // "75%" or "N/A"
    string? AvgDuration, // "2h 30m" or "N/A"

    // Recent sessions (last 5)
    GameSessionDto[]? RecentSessions = null,

    // Setup checklist
    GameChecklistItemDto[]? Checklist = null,

    // Custom configurations
    AgentConfigDto? CustomAgentConfig = null,
    CustomPdfDto? CustomPdf = null,

    // Labels
    LabelDto[]? Labels = null,

    // RAG / KB access (mirrored from UserLibraryEntryDto for consistency)
    bool HasRagAccess = false,       // computed: admin || IsRagPublic || OwnershipDeclaredAt != null
    bool HasKb = false,              // true if >= 1 PDF fully indexed in RAG
    int KbCardCount = 0,             // total PDF documents linked to this game
    int KbIndexedCount = 0,          // PDF documents with ProcessingState.Ready
    int KbProcessingCount = 0,       // PDF documents currently in pipeline
    DateTime? OwnershipDeclaredAt = null
);

/// <summary>
/// DTO for a recorded game session.
/// </summary>
internal record GameSessionDto(
    Guid Id,
    DateTime PlayedAt,
    int DurationMinutes,
    string DurationFormatted, // "2h 30m"
    bool? DidWin,
    string? Players,
    string? Notes
);

/// <summary>
/// DTO for a checklist item.
/// </summary>
internal record GameChecklistItemDto(
    Guid Id,
    string Description,
    int Order,
    bool IsCompleted,
    string? AdditionalInfo
);

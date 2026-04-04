namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

/// <summary>
/// DTO for a single library entry with game information.
/// Issue #3663: Updated to support both SharedGame and PrivateGame references.
/// Issue #4998: Replaced HasPdfDocuments with KB-aware fields (hasKb = at least 1 indexed PDF in RAG).
/// </summary>
internal record UserLibraryEntryDto(
    Guid Id,
    Guid UserId,
    Guid GameId, // Backwards compatible: SharedGameId or PrivateGameId
    string GameTitle,
    string? GamePublisher,
    int? GameYearPublished,
    string? GameIconUrl,
    string? GameImageUrl,
    DateTime AddedAt,
    string? Notes,
    bool IsFavorite,
    string CurrentState,
    DateTime? StateChangedAt = null,
    string? StateNotes = null,
    AgentConfigDto? CustomAgentConfig = null,
    CustomPdfDto? CustomPdf = null,
    bool HasKb = false,              // Issue #4998: true if >= 1 PDF fully indexed in RAG (ProcessingState.Ready)
    int KbCardCount = 0,             // Issue #4998: total PDF documents linked to this game
    int KbIndexedCount = 0,          // Issue #4998: PDF documents with ProcessingState.Ready
    int KbProcessingCount = 0,       // Issue #4998: PDF documents currently in pipeline (not Ready/Failed)
    bool AgentIsOwned = true,        // Issue #4998: current user owns the agent for this game (always true in library context)
    int? MinPlayers = null,          // Game metadata from SharedGame for card back
    int? MaxPlayers = null,
    int? PlayingTimeMinutes = null,
    decimal? ComplexityRating = null,
    decimal? AverageRating = null,
    Guid? PrivateGameId = null,      // Issue #3663: Private game reference
    bool IsPrivateGame = false,      // Issue #3663: Computed flag
    bool CanProposeToCatalog = false, // Issue #3663: Can propose private game to catalog
    DateTime? OwnershipDeclaredAt = null, // Ownership/RAG access: when user declared ownership of this game
    bool HasRagAccess = false             // Ownership/RAG access: computed from admin/public/ownership rules
);

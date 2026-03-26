namespace Api.Routing;

/// <summary>
/// Request body for adding a game to library.
/// </summary>
public record AddGameToLibraryRequest(
    string? Notes = null,
    bool IsFavorite = false
);

/// <summary>
/// Request body for updating a library entry.
/// </summary>
public record UpdateLibraryEntryRequest(
    string? Notes = null,
    bool? IsFavorite = null
);

/// <summary>
/// Request body for uploading custom PDF rulebook.
/// </summary>
public record UploadCustomPdfRequest(
    string PdfUrl,
    long FileSizeBytes,
    string OriginalFileName
);

/// <summary>
/// Request body for creating a library share link.
/// </summary>
public record CreateLibraryShareRequest(
    string PrivacyLevel,
    bool IncludeNotes = false,
    DateTime? ExpiresAt = null
);

/// <summary>
/// Request body for updating a library share link.
/// </summary>
public record UpdateLibraryShareRequest(
    string? PrivacyLevel = null,
    bool? IncludeNotes = null,
    DateTime? ExpiresAt = null
);

/// <summary>
/// Request body for updating game state.
/// </summary>
public record UpdateGameStateRequest(
    string NewState,
    string? StateNotes = null
);

/// <summary>
/// Request body for recording game session.
/// </summary>
public record RecordGameSessionRequest(
    DateTime PlayedAt,
    int DurationMinutes,
    bool? DidWin = null,
    string? Players = null,
    string? Notes = null
);

/// <summary>
/// Request body for sending loan reminder.
/// </summary>
public record SendLoanReminderRequest(
    string? CustomMessage = null
);

/// <summary>
/// Request body for saving agent configuration (Issue #3212).
/// Simplified version of AgentConfigDto for frontend modal.
/// </summary>
public record SaveAgentConfigRequest(
    Guid TypologyId,
    string ModelName,
    double CostEstimate
);

/// <summary>
/// Request body for creating game agent with custom typology and strategy (Issue #5).
/// </summary>
public record CreateGameAgentRequest(
    Guid TypologyId,
    string StrategyName,
    string? StrategyParameters = null
);

/// <summary>
/// Request body for creating a custom label (Epic #3511).
/// </summary>
public record CreateCustomLabelRequest(
    string Name,
    string Color
);

/// <summary>
/// Request body for adding an entity to collection.
/// Issue #4263: Phase 2 - Generic UserCollection System
/// </summary>
public record AddToCollectionRequest(
    string? Notes = null,
    bool IsFavorite = false
);

/// <summary>
/// Request body for bulk adding entities to collection.
/// Issue #4268: Phase 3 - Bulk Collection Actions
/// </summary>
public record BulkAddToCollectionRequest(
    IReadOnlyList<Guid> EntityIds,
    string? Notes = null,
    bool IsFavorite = false
);

/// <summary>
/// Request body for bulk removing entities from collection.
/// Issue #4268: Phase 3 - Bulk Collection Actions
/// </summary>
public record BulkRemoveFromCollectionRequest(
    IReadOnlyList<Guid> EntityIds
);

/// <summary>
/// Request body for getting bulk associated data.
/// Issue #4268: Phase 3 - Bulk Collection Actions
/// </summary>
public record BulkGetAssociatedDataRequest(
    IReadOnlyList<Guid> EntityIds
);

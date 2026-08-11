

#pragma warning disable MA0048 // File name must match type name - Contains related Request/Response DTOs
namespace Api.BoundedContexts.GameManagement.Application.DTOs;

/// <summary>
/// Data transfer object for game information.
/// Issue #2373: Added SharedGameId for catalog integration.
/// </summary>
/// <summary>
/// Data transfer object for game information.
/// Issue #2373: Added SharedGameId for catalog integration.
/// Issue #3481: Added publication workflow fields.
/// </summary>
internal record GameDto(
    Guid Id,
    string Title,
    string? Publisher,
    int? YearPublished,
    int? MinPlayers,
    int? MaxPlayers,
    int? MinPlayTimeMinutes,
    int? MaxPlayTimeMinutes,
    int? BggId,
    DateTime CreatedAt,
    string? IconUrl = null,
    string? ImageUrl = null,
    Guid? SharedGameId = null,
    bool IsPublished = false,
    string? ApprovalStatus = null,
    DateTime? PublishedAt = null,
    // Issue #2243 (epic #2242) Block C: expose the denormalized HasKnowledgeBase flag so
    // /api/v1/games clients (admin grid + user library) can render "agente pronto" badges
    // without a second round-trip to /api/v1/shared-games. Default false for backward-compat.
    bool HasKnowledgeBase = false
);

/// <summary>
/// Extended DTO for game detail page with additional metadata and statistics.
/// Issue #2373: Added SharedGameId for catalog integration.
/// </summary>
internal record GameDetailsDto(
    Guid Id,
    string Title,
    string? Publisher,
    int? YearPublished,
    int? MinPlayers,
    int? MaxPlayers,
    int? MinPlayTimeMinutes,
    int? MaxPlayTimeMinutes,
    int? BggId,
    string? BggMetadata,
    DateTime CreatedAt,
    // Extended metadata
    bool SupportsSolo,
    // Play statistics (optional - null if no sessions exist)
    int? TotalSessionsPlayed,
    DateTime? LastPlayedAt,
    // Game images
    string? IconUrl = null,
    string? ImageUrl = null,
    // SharedGameCatalog integration
    Guid? SharedGameId = null,
    // Issue #2243 (epic #2242) Block C: expose HasKnowledgeBase + KbsCount so
    // game detail pages can render "agente pronto" badge and "chunks indexed" stats
    // without a second /api/v1/shared-games round-trip.
    bool HasKnowledgeBase = false,
    int KbsCount = 0
);

/// <summary>
/// DTO for rule atom (atomic rule element).
/// </summary>
internal record RuleAtomDto(
    string Id,
    string Text,
    string? Section,
    string? Page,
    string? Line
);

/// <summary>
/// DTO for rule specification.
/// Issue #2055: Includes ETag for optimistic concurrency control.
/// </summary>
/// <param name="Id">Unique identifier for this rule specification.</param>
/// <param name="GameId">ID of the game this specification belongs to.</param>
/// <param name="Version">Version string of the specification.</param>
/// <param name="CreatedAt">Timestamp when this version was created.</param>
/// <param name="CreatedByUserId">Optional ID of the user who created this version.</param>
/// <param name="ParentVersionId">Optional ID of the parent version for branching.</param>
/// <param name="Atoms">List of rule atoms defining the specification.</param>
/// <param name="ETag">Issue #2055: ETag (base64-encoded RowVersion) for optimistic concurrency. Send this value back when updating to detect concurrent modifications.</param>
internal record RuleSpecDto(
    Guid Id,
    Guid GameId,
    string Version,
    DateTime CreatedAt,
    Guid? CreatedByUserId,
    Guid? ParentVersionId,
    IReadOnlyList<RuleAtomDto> Atoms,
    string? ETag = null
);

/// <summary>
/// Issue #2055: Lock status information for collaborative editing.
/// </summary>
internal record EditorLockDto(
    Guid GameId,
    Guid? LockedByUserId,
    string? LockedByUserEmail,
    DateTime? LockedAt,
    DateTime? ExpiresAt,
    bool IsLocked,
    bool IsCurrentUserLock
);

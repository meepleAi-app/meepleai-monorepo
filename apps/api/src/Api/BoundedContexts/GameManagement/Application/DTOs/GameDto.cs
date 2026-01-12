

#pragma warning disable MA0048 // File name must match type name - Contains related Request/Response DTOs
namespace Api.BoundedContexts.GameManagement.Application.DTOs;

/// <summary>
/// Data transfer object for game information.
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
    string? ImageUrl = null
);

/// <summary>
/// DTO for creating a game.
/// </summary>
internal record CreateGameRequest(
    string Title,
    string? Publisher = null,
    int? YearPublished = null,
    int? MinPlayers = null,
    int? MaxPlayers = null,
    int? MinPlayTimeMinutes = null,
    int? MaxPlayTimeMinutes = null,
    string? IconUrl = null,
    string? ImageUrl = null,
    int? BggId = null
);

/// <summary>
/// DTO for updating game details.
/// </summary>
internal record UpdateGameRequest(
    string? Title = null,
    string? Publisher = null,
    int? YearPublished = null,
    int? MinPlayers = null,
    int? MaxPlayers = null,
    int? MinPlayTimeMinutes = null,
    int? MaxPlayTimeMinutes = null
);

/// <summary>
/// Extended DTO for game detail page with additional metadata and statistics.
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
    string? ImageUrl = null
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

/// <summary>
/// Issue #2055: Conflict information when concurrent edit is detected.
/// </summary>
internal record RuleSpecConflictDto(
    RuleSpecDto LocalVersion,
    RuleSpecDto RemoteVersion,
    string ConflictReason
);

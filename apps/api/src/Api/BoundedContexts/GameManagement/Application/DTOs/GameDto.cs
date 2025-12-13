

#pragma warning disable MA0048 // File name must match type name - Contains related Request/Response DTOs
namespace Api.BoundedContexts.GameManagement.Application.DTOs;

/// <summary>
/// Data transfer object for game information.
/// </summary>
public record GameDto(
    Guid Id,
    string Title,
    string? Publisher,
    int? YearPublished,
    int? MinPlayers,
    int? MaxPlayers,
    int? MinPlayTimeMinutes,
    int? MaxPlayTimeMinutes,
    int? BggId,
    DateTime CreatedAt
);

/// <summary>
/// DTO for creating a game.
/// </summary>
public record CreateGameRequest(
    string Title,
    string? Publisher = null,
    int? YearPublished = null,
    int? MinPlayers = null,
    int? MaxPlayers = null,
    int? MinPlayTimeMinutes = null,
    int? MaxPlayTimeMinutes = null
);

/// <summary>
/// DTO for updating game details.
/// </summary>
public record UpdateGameRequest(
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
public record GameDetailsDto(
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
    DateTime? LastPlayedAt
);

/// <summary>
/// DTO for rule atom (atomic rule element).
/// </summary>
public record RuleAtomDto(
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
public record RuleSpecDto(
    Guid Id,
    Guid GameId,
    string Version,
    DateTime CreatedAt,
    Guid? CreatedByUserId,
    Guid? ParentVersionId,
    IReadOnlyList<RuleAtomDto> Atoms,
    /// <summary>
    /// Issue #2055: ETag (base64-encoded RowVersion) for optimistic concurrency.
    /// Send this value back when updating to detect concurrent modifications.
    /// </summary>
    string? ETag = null
);

/// <summary>
/// Issue #2055: Lock status information for collaborative editing.
/// </summary>
public record EditorLockDto(
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
public record RuleSpecConflictDto(
    RuleSpecDto LocalVersion,
    RuleSpecDto RemoteVersion,
    string ConflictReason
);

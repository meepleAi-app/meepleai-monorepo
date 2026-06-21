namespace Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;

/// <summary>
/// DTO representing a single version snapshot of a PlayRecord's editable details.
/// Returned by GET /play-records/{id}/versions (creator-only, last 5 DESC).
/// Issue #2437-3: version history + restore.
/// </summary>
public record PlayRecordVersionDto(
    int VersionNumber,
    DateTime SessionDate,
    string? Notes,
    string? Location,
    DateTime CreatedAt,
    Guid CreatedByUserId);

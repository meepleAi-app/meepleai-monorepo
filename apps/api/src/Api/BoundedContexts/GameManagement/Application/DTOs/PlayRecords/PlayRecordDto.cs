using Api.BoundedContexts.GameManagement.Domain.Enums;

namespace Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;

/// <summary>
/// DTO for full play record details including players and scores.
/// Issue #3890: CQRS queries for play records.
/// Issue #1663: Phase 1 – WinnerPlayerIds and OutcomeType computed on read.
/// Issue #2436 PR-C: Photos (presigned read-path) added as last parameter.
/// Issue #2437-1: Xmin concurrency token exposed so clients can round-trip it on update.
/// Issue #2437-2: ShareToken exposed so the authenticated detail view can show/revoke the link.
/// </summary>
public record PlayRecordDto(
    Guid Id,
    Guid? GameId,
    string GameName,
    DateTime SessionDate,
    TimeSpan? Duration,
    PlayRecordStatus Status,
    List<SessionPlayerDto> Players,
    SessionScoringConfigDto ScoringConfig,
    Guid CreatedByUserId,
    PlayRecordVisibility Visibility,
    DateTime? StartTime,
    DateTime? EndTime,
    string? Notes,
    string? Location,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<Guid> WinnerPlayerIds,
    string OutcomeType,
    IReadOnlyList<PlayRecordPhotoDto> Photos,
    uint Xmin,
    string? ShareToken
);

/// <summary>
/// A photo attached to a play record, exposed for read (#2436 PR-C).
/// <c>Url</c>/<c>ThumbnailUrl</c> are presigned download URLs (or raw paths on local storage).
/// </summary>
public record PlayRecordPhotoDto(
    Guid Id,
    string Url,
    string? ThumbnailUrl,
    string? OcrText,
    string? Caption,
    Guid UploadedByUserId,
    DateTime UploadedAt
);

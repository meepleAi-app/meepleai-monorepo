using Api.BoundedContexts.GameManagement.Domain.Enums;

namespace Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;

/// <summary>
/// DTO for full play record details including players and scores.
/// Issue #3890: CQRS queries for play records.
/// Issue #1663: Phase 1 – WinnerPlayerIds and OutcomeType computed on read.
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
    string OutcomeType
);

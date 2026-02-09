using Api.BoundedContexts.GameManagement.Domain.Enums;

namespace Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;

/// <summary>
/// DTO for full play record details including players and scores.
/// Issue #3890: CQRS queries for play records.
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
    DateTime UpdatedAt
);

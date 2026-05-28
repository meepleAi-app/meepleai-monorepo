namespace Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;

/// <summary>
/// DTO for player in a play record.
/// Issue #3890: CQRS queries for play records.
/// Issue #1663: Phase 1 – TotalScore computed on read from "points" dimension.
/// </summary>
public record SessionPlayerDto(
    Guid Id,
    Guid? UserId,
    string DisplayName,
    List<SessionScoreDto> Scores,
    int? TotalScore
);

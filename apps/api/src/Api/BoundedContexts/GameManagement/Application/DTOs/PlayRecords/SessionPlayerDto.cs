namespace Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;

/// <summary>
/// DTO for player in a play record.
/// Issue #3890: CQRS queries for play records.
/// </summary>
public record SessionPlayerDto(
    Guid Id,
    Guid? UserId,
    string DisplayName,
    List<SessionScoreDto> Scores
);

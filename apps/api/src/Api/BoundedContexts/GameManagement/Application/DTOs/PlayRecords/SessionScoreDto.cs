namespace Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;

/// <summary>
/// DTO for a score in a play record.
/// Issue #3890: CQRS queries for play records.
/// </summary>
public record SessionScoreDto(
    string Dimension,
    int Value,
    string? Unit
);

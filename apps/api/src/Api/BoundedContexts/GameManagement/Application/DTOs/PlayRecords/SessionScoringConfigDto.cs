namespace Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;

/// <summary>
/// DTO for scoring configuration.
/// Issue #3890: CQRS queries for play records.
/// </summary>
public record SessionScoringConfigDto(
    List<string> EnabledDimensions,
    Dictionary<string, string> DimensionUnits
);

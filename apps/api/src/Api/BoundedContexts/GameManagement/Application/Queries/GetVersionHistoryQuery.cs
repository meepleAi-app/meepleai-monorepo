using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Query with Result record
namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Query to retrieve version history for a rule specification.
/// </summary>
public record GetVersionHistoryQuery(
    Guid GameId
) : IQuery<RuleSpecHistoryDto>;

/// <summary>
/// DTO for rule specification version history.
/// </summary>
public record RuleSpecHistoryDto(
    Guid GameId,
    IReadOnlyList<RuleSpecVersionDto> Versions,
    int TotalVersions
);

/// <summary>
/// DTO for a single version in the history.
/// </summary>
public record RuleSpecVersionDto(
    string Version,
    DateTime CreatedAt,
    int AtomCount,
    string? CreatedByUserName
);

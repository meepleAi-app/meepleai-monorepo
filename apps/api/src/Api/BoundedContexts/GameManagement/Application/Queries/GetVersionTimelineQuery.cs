using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Query with Result record
namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Query to retrieve version timeline with filtering and branching support.
/// </summary>
internal record GetVersionTimelineQuery(
    Guid GameId,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    string? Author = null,
    string? SearchQuery = null
) : IQuery<VersionTimelineDto>;

/// <summary>
/// DTO for version timeline response.
/// </summary>
internal record VersionTimelineDto(
    Guid GameId,
    IReadOnlyList<VersionNodeDto> Versions,
    int TotalVersions,
    IReadOnlyList<string> Authors
);

/// <summary>
/// DTO for a version node in the timeline.
/// </summary>
internal record VersionNodeDto(
    Guid Id,
    string Version,
    string Title,
    string Description,
    string Author,
    DateTime CreatedAt,
    Guid? ParentVersionId,
    string? ParentVersion,
    IReadOnlyList<Guid>? MergedFromVersionIds,
    IReadOnlyList<string>? MergedFromVersions,
    int ChangeCount,
    bool IsCurrentVersion
);

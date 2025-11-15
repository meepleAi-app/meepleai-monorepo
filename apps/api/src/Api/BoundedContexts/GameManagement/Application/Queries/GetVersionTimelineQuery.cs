using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Query to retrieve version timeline with filtering and branching support.
/// </summary>
public record GetVersionTimelineQuery(
    Guid GameId,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    string? Author = null,
    string? SearchQuery = null
) : IQuery<VersionTimelineDto>;

/// <summary>
/// DTO for version timeline response.
/// </summary>
public record VersionTimelineDto(
    Guid GameId,
    IReadOnlyList<VersionNodeDto> Versions,
    int TotalVersions,
    IReadOnlyList<string> Authors
);

/// <summary>
/// DTO for a version node in the timeline.
/// </summary>
public record VersionNodeDto(
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

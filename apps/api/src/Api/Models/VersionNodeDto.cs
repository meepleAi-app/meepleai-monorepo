

#pragma warning disable MA0048 // File name must match type name - Contains related Request/Response DTOs
namespace Api.Models;

/// <summary>
/// EDIT-06: Version timeline node with branching and merging support
/// </summary>
public record VersionNodeDto
{
    public Guid Id { get; init; }
    public string Version { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }

    // Branching support
    public Guid? ParentVersionId { get; init; }
    public string? ParentVersion { get; init; }

    // Merging support
    public List<Guid> MergedFromVersionIds { get; init; } = new();
    public List<string> MergedFromVersions { get; init; } = new();

    // Timeline metadata
    public string? ThumbnailUrl { get; init; }
    public int ChangeCount { get; init; }
    public bool IsCurrentVersion { get; init; }
}

/// <summary>
/// Timeline filter parameters
/// </summary>
public record VersionTimelineFilters
{
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string? Author { get; init; }
    public string? SearchQuery { get; init; }
}

/// <summary>
/// Timeline response with versions
/// </summary>
public record VersionTimelineResponse
{
    public string GameId { get; init; } = string.Empty;
    public List<VersionNodeDto> Versions { get; init; } = new();
    public int TotalVersions { get; init; }
    public List<string> Authors { get; init; } = new();
}

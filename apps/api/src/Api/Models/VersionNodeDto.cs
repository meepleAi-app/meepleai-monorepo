

#pragma warning disable MA0048 // File name must match type name - Contains related Request/Response DTOs
namespace Api.Models;

/// <summary>
/// EDIT-06: Version timeline node with branching and merging support
/// </summary>
internal record VersionNodeDto
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
    public IList<Guid> MergedFromVersionIds { get; init; } = new List<Guid>();
    public IList<string> MergedFromVersions { get; init; } = new List<string>();

    // Timeline metadata
    public string? ThumbnailUrl { get; init; }
    public int ChangeCount { get; init; }
    public bool IsCurrentVersion { get; init; }
}

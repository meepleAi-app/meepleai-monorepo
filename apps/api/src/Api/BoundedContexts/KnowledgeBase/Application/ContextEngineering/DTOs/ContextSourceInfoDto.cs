namespace Api.BoundedContexts.KnowledgeBase.Application.ContextEngineering.DTOs;

/// <summary>
/// DTO for context source information.
/// Issue #3491: Context Engineering Framework Implementation.
/// </summary>
public sealed record ContextSourceInfoDto
{
    /// <summary>
    /// Unique identifier for the source.
    /// </summary>
    public required string SourceId { get; init; }

    /// <summary>
    /// Display name for the source.
    /// </summary>
    public required string SourceName { get; init; }

    /// <summary>
    /// Default priority (0-100, higher = more important).
    /// </summary>
    public int DefaultPriority { get; init; }

    /// <summary>
    /// Whether the source is currently available.
    /// </summary>
    public bool IsAvailable { get; init; }

    /// <summary>
    /// Description of what this source provides.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Type of content this source provides.
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// Retrieval strategy used for this source.
    /// </summary>
    public string? Strategy { get; init; }
}

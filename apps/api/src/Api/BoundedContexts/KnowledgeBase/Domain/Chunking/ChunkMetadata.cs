namespace Api.BoundedContexts.KnowledgeBase.Domain.Chunking;

/// <summary>
/// ADR-016 Phase 1: Metadata for a chunk containing positional and semantic information.
/// Value Object - immutable, equality by value.
/// </summary>
public sealed record ChunkMetadata
{
    /// <summary>
    /// Page number where the chunk originates (1-based).
    /// </summary>
    public int Page { get; init; }

    /// <summary>
    /// Nearest section heading above this chunk.
    /// </summary>
    public string? Heading { get; init; }

    /// <summary>
    /// Optional bounding box for spatial info (PDF coordinates).
    /// </summary>
    public BoundingBox? BBox { get; init; }

    /// <summary>
    /// Element type from Unstructured extraction (text, table, list, heading).
    /// </summary>
    public string ElementType { get; init; } = "text";

    /// <summary>
    /// Associated game identifier.
    /// </summary>
    public Guid? GameId { get; init; }

    /// <summary>
    /// Source document identifier.
    /// </summary>
    public Guid DocumentId { get; init; }

    /// <summary>
    /// Character start position in original document.
    /// </summary>
    public int CharStart { get; init; }

    /// <summary>
    /// Character end position in original document.
    /// </summary>
    public int CharEnd { get; init; }
}

/// <summary>
/// Bounding box for spatial information from PDF extraction.
/// </summary>
public sealed record BoundingBox
{
    public float X { get; init; }
    public float Y { get; init; }
    public float Width { get; init; }
    public float Height { get; init; }

    /// <summary>
    /// Creates a bounding box from coordinates.
    /// </summary>
    public static BoundingBox FromCoordinates(float x, float y, float width, float height)
    {
        return new BoundingBox
        {
            X = x,
            Y = y,
            Width = width,
            Height = height
        };
    }
}

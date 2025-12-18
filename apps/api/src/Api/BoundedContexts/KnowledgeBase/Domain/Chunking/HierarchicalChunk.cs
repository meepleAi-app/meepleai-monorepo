namespace Api.BoundedContexts.KnowledgeBase.Domain.Chunking;

/// <summary>
/// ADR-016 Phase 1: Hierarchical chunk entity with parent/child relationships.
/// Entity - identity by Id.
/// </summary>
internal sealed class HierarchicalChunk
{
    /// <summary>
    /// Unique identifier for this chunk.
    /// </summary>
    public string Id { get; private set; }

    /// <summary>
    /// Parent chunk identifier (null for root/section-level chunks).
    /// </summary>
    public string? ParentId { get; private set; }

    /// <summary>
    /// List of child chunk identifiers.
    /// </summary>
    public IList<string> ChildIds { get; private set; } = new List<string>();

    /// <summary>
    /// Chunk text content.
    /// </summary>
    public string Content { get; private set; }

    /// <summary>
    /// Hierarchy level: 0=section, 1=paragraph, 2=sentence.
    /// </summary>
    public int Level { get; private set; }

    /// <summary>
    /// Chunk metadata (page, heading, element type, etc.).
    /// </summary>
    public ChunkMetadata Metadata { get; private set; }

    /// <summary>
    /// Created timestamp.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Private constructor for EF Core and factory methods.
    /// </summary>
    private HierarchicalChunk()
    {
        Id = string.Empty;
        Content = string.Empty;
        Metadata = new ChunkMetadata();
    }

    /// <summary>
    /// Creates a new hierarchical chunk.
    /// </summary>
    public static HierarchicalChunk Create(
        string content,
        int level,
        ChunkMetadata metadata,
        string? parentId = null)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be empty", nameof(content));

        if (level < 0 || level > 2)
            throw new ArgumentOutOfRangeException(nameof(level), "Level must be 0, 1, or 2");

        ArgumentNullException.ThrowIfNull(metadata);

        return new HierarchicalChunk
        {
            Id = Guid.NewGuid().ToString("N"),
            ParentId = parentId,
            Content = content,
            Level = level,
            Metadata = metadata,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a parent (section) chunk at level 0.
    /// </summary>
    public static HierarchicalChunk CreateParent(
        string content,
        ChunkMetadata metadata)
    {
        return Create(content, level: 0, metadata, parentId: null);
    }

    /// <summary>
    /// Creates a child chunk linked to a parent.
    /// </summary>
    public static HierarchicalChunk CreateChild(
        string content,
        int level,
        ChunkMetadata metadata,
        string parentId)
    {
        if (string.IsNullOrWhiteSpace(parentId))
            throw new ArgumentException("ParentId is required for child chunks", nameof(parentId));

        return Create(content, level, metadata, parentId);
    }

    /// <summary>
    /// Adds a child chunk ID to this chunk.
    /// </summary>
    public void AddChild(string childId)
    {
        if (string.IsNullOrWhiteSpace(childId))
            throw new ArgumentException("ChildId cannot be empty", nameof(childId));

        if (!ChildIds.Contains(childId, StringComparer.Ordinal))
        {
            ChildIds.Add(childId);
        }
    }

    /// <summary>
    /// Checks if this chunk is a root (parent) chunk.
    /// </summary>
    public bool IsRoot => ParentId == null;

    /// <summary>
    /// Checks if this chunk has children.
    /// </summary>
    public bool HasChildren => ChildIds.Count > 0;

    /// <summary>
    /// Gets the token count estimate for this chunk.
    /// </summary>
    public int EstimatedTokenCount => (int)Math.Ceiling(Content.Length / 4.0);
}

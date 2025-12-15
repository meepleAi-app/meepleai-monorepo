using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Indexing;

/// <summary>
/// ADR-016 Phase 3: Payload structure for Qdrant points.
/// Contains metadata for filtering, parent/child relationships, and context retrieval.
/// Value Object - immutable, equality by value.
/// </summary>
internal sealed class ChunkPayload : ValueObject
{
    /// <summary>
    /// Game identifier for filtering (stored as string for Qdrant keyword index).
    /// </summary>
    public string GameId { get; }

    /// <summary>
    /// PDF document identifier.
    /// </summary>
    public string PdfId { get; }

    /// <summary>
    /// Page number in the source document (1-based).
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// Chunk index within the document.
    /// </summary>
    public int ChunkIndex { get; }

    /// <summary>
    /// Hierarchy level: 0=section, 1=paragraph, 2=sentence.
    /// </summary>
    public int Level { get; }

    /// <summary>
    /// Parent chunk ID for hierarchy navigation (null for root chunks).
    /// </summary>
    public string? ParentChunkId { get; }

    /// <summary>
    /// List of child chunk IDs for hierarchy expansion.
    /// </summary>
    public IReadOnlyList<string> ChildChunkIds { get; }

    /// <summary>
    /// Document category for filtering (e.g., "rules", "reference", "tutorial").
    /// </summary>
    public string Category { get; }

    /// <summary>
    /// Document language code (e.g., "it", "en").
    /// </summary>
    public string Language { get; }

    /// <summary>
    /// Heading or title associated with this chunk.
    /// </summary>
    public string? Heading { get; }

    /// <summary>
    /// Element type from document structure (e.g., "Title", "NarrativeText", "ListItem", "Table").
    /// </summary>
    public string ElementType { get; }

    /// <summary>
    /// Estimated token count for context window management.
    /// </summary>
    public int TokenCount { get; }

    private ChunkPayload(
        string gameId,
        string pdfId,
        int pageNumber,
        int chunkIndex,
        int level,
        string? parentChunkId,
        IReadOnlyList<string> childChunkIds,
        string category,
        string language,
        string? heading,
        string elementType,
        int tokenCount)
    {
        GameId = gameId;
        PdfId = pdfId;
        PageNumber = pageNumber;
        ChunkIndex = chunkIndex;
        Level = level;
        ParentChunkId = parentChunkId;
        ChildChunkIds = childChunkIds;
        Category = category;
        Language = language;
        Heading = heading;
        ElementType = elementType;
        TokenCount = tokenCount;
    }

    /// <summary>
    /// Creates an empty payload (for deserialization).
    /// </summary>
    public static ChunkPayload Empty() => new(
        gameId: string.Empty,
        pdfId: string.Empty,
        pageNumber: 0,
        chunkIndex: 0,
        level: 0,
        parentChunkId: null,
        childChunkIds: [],
        category: "unknown",
        language: "it",
        heading: null,
        elementType: "Unknown",
        tokenCount: 0
    );

    /// <summary>
    /// Creates a payload from a hierarchical chunk and document context.
    /// </summary>
    public static ChunkPayload Create(
        Guid gameId,
        Guid pdfDocumentId,
        int pageNumber,
        int chunkIndex,
        int level,
        string? parentChunkId = null,
        IReadOnlyList<string>? childChunkIds = null,
        string category = "rules",
        string language = "it",
        string? heading = null,
        string elementType = "NarrativeText",
        int tokenCount = 0)
    {
        return new ChunkPayload(
            gameId: gameId.ToString(),
            pdfId: pdfDocumentId.ToString(),
            pageNumber: pageNumber,
            chunkIndex: chunkIndex,
            level: level,
            parentChunkId: parentChunkId,
            childChunkIds: childChunkIds ?? [],
            category: category,
            language: language,
            heading: heading,
            elementType: elementType,
            tokenCount: tokenCount
        );
    }

    /// <summary>
    /// Creates a root/section-level payload (no parent).
    /// </summary>
    public static ChunkPayload CreateRoot(
        Guid gameId,
        Guid pdfDocumentId,
        int pageNumber,
        int chunkIndex,
        IReadOnlyList<string>? childChunkIds = null,
        string category = "rules",
        string language = "it",
        string? heading = null,
        string elementType = "Title",
        int tokenCount = 0)
    {
        return Create(
            gameId: gameId,
            pdfDocumentId: pdfDocumentId,
            pageNumber: pageNumber,
            chunkIndex: chunkIndex,
            level: 0,
            parentChunkId: null,
            childChunkIds: childChunkIds,
            category: category,
            language: language,
            heading: heading,
            elementType: elementType,
            tokenCount: tokenCount
        );
    }

    /// <summary>
    /// Creates a child payload linked to a parent.
    /// </summary>
    public static ChunkPayload CreateChild(
        Guid gameId,
        Guid pdfDocumentId,
        int pageNumber,
        int chunkIndex,
        int level,
        string parentChunkId,
        string category = "rules",
        string language = "it",
        string? heading = null,
        string elementType = "NarrativeText",
        int tokenCount = 0)
    {
        if (string.IsNullOrWhiteSpace(parentChunkId))
            throw new ArgumentException("ParentChunkId is required for child payloads", nameof(parentChunkId));

        return Create(
            gameId: gameId,
            pdfDocumentId: pdfDocumentId,
            pageNumber: pageNumber,
            chunkIndex: chunkIndex,
            level: level,
            parentChunkId: parentChunkId,
            childChunkIds: null,
            category: category,
            language: language,
            heading: heading,
            elementType: elementType,
            tokenCount: tokenCount
        );
    }

    /// <summary>
    /// Creates a new payload with updated child chunk IDs.
    /// </summary>
    public ChunkPayload WithChildren(IReadOnlyList<string> childChunkIds)
    {
        return new ChunkPayload(
            gameId: GameId,
            pdfId: PdfId,
            pageNumber: PageNumber,
            chunkIndex: ChunkIndex,
            level: Level,
            parentChunkId: ParentChunkId,
            childChunkIds: childChunkIds,
            category: Category,
            language: Language,
            heading: Heading,
            elementType: ElementType,
            tokenCount: TokenCount
        );
    }

    /// <summary>
    /// Indicates if this is a root (parent) chunk with no parent.
    /// </summary>
    public bool IsRoot => ParentChunkId == null;

    /// <summary>
    /// Indicates if this chunk has children.
    /// </summary>
    public bool HasChildren => ChildChunkIds.Count > 0;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return GameId;
        yield return PdfId;
        yield return PageNumber;
        yield return ChunkIndex;
        yield return Level;
        yield return ParentChunkId;
        foreach (var childId in ChildChunkIds)
        {
            yield return childId;
        }
        yield return Category;
        yield return Language;
        yield return Heading;
        yield return ElementType;
        yield return TokenCount;
    }

    public override string ToString()
    {
        var gameIdDisplay = GameId.Length >= 8 ? $"{GameId[..8]}..." : GameId;
        return $"Payload(game={gameIdDisplay}, page={PageNumber}, level={Level}, children={ChildChunkIds.Count})";
    }
}

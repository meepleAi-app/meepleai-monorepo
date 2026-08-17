using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Domain.Chunking;

namespace Api.Services;

/// <summary>
/// Document chunk with embedding.
/// Used across multiple bounded contexts (RAG, hybrid search, indexing).
/// </summary>
internal record DocumentChunk
{
    // Slice D: stable identity so a child chunk's ParentChunkId can reference
    // its parent's persisted TextChunkEntity.Id. Guid.Empty (default) means
    // "not yet assigned" — save sites fall back to a fresh Guid in that case.
    public Guid Id { get; init; }
    public string Text { get; init; } = string.Empty;
    public float[] Embedding { get; init; } = Array.Empty<float>();
    public int Page { get; init; }
    public int CharStart { get; init; }
    public int CharEnd { get; init; }
    // Issue #730: hierarchy fields — null/defaults when not set by chunking pipeline
    public string? Heading { get; init; }
    public short Level { get; init; } = 1;
    public Guid? ParentChunkId { get; init; }
    public string ElementType { get; init; } = "NarrativeText";
    // SP-B (#3406): normalized region [0,1] top-left (union of the section's element boxes on
    // the start page); null when the extractor emitted no coordinates.
    public BoundingBox? BBox { get; init; }
}

/// <summary>
/// Result of indexing operation.
/// </summary>
internal record IndexResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int IndexedCount { get; init; }
    public static IndexResult CreateSuccess(int count) =>
        new() { Success = true, IndexedCount = count };
    public static IndexResult CreateFailure(string error) =>
        new() { Success = false, ErrorMessage = error };
}

/// <summary>
/// Result of vector search operation (pgvector-backed).
/// </summary>
internal record SearchResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public List<SearchResultItem> Results { get; init; } = new();
    public static SearchResult CreateSuccess(List<SearchResultItem> results) =>
        new() { Success = true, Results = results };
    public static SearchResult CreateFailure(string error) =>
        new() { Success = false, ErrorMessage = error };
}

/// <summary>
/// Single search result item.
/// </summary>
internal record SearchResultItem
{
    public float Score { get; init; }
    public string Text { get; init; } = string.Empty;
    public string PdfId { get; init; } = string.Empty;
    public int Page { get; init; }
    public int ChunkIndex { get; init; }

    /// <summary>
    /// Role classification of the underlying chunk (multi-label bitflag from text_chunks.role_tags,
    /// denormalized onto pgvector_embeddings.role_tags). Carried through the vector-arm projection so
    /// HYBRID fusion can apply the role-match boost to vector-only chunks (Slice C).
    /// </summary>
    public GameBookRole RoleTags { get; init; } = GameBookRole.None;

    /// <summary>#3270: chunk heading-path label for the heading-match boost (nullable).</summary>
    public string? Heading { get; init; }

    /// <summary>
    /// #3740: ISO 639-1 language of the chunk, from <c>pgvector_embeddings.lang</c>. Carried through
    /// so the cross-game fusion can normalise the cosine <b>within</b> each language instead of
    /// across the whole aggregate — see <c>MultiGameHybridSearchService.FuseGlobally</c>.
    /// Null for the keyword/FTS arm, which reads <c>text_chunks</c> and has no language column.
    /// </summary>
    public string? Language { get; init; }

    /// <summary>
    /// SP-C (#3407): raw bounding-box JSON carried from the vector arm (<c>Embedding.BoundingBoxesJson</c>).
    /// Primitive string (no layer dep); parsed + Full-gated at the API handler boundary. Null for the
    /// keyword/FTS/RAPTOR arms and the pre-coordinate corpus.
    /// </summary>
    public string? BoundingBoxesJson { get; init; }

    /// <summary>SP-C (#3407): chunk char offsets in the source PDF text (nullable).</summary>
    public int? CharStart { get; init; }
    public int? CharEnd { get; init; }
}

/// <summary>
/// Result of chess knowledge indexing operation.
/// </summary>
internal record ChessIndexResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int TotalKnowledgeItems { get; init; }
    public int TotalChunks { get; init; }
    public Dictionary<string, int> CategoryCounts { get; init; } = new(StringComparer.Ordinal);

    public static ChessIndexResult CreateSuccess(int totalItems, int totalChunks, Dictionary<string, int> categoryCounts) =>
        new() { Success = true, TotalKnowledgeItems = totalItems, TotalChunks = totalChunks, CategoryCounts = categoryCounts };

    public static ChessIndexResult CreateFailure(string error) =>
        new() { Success = false, ErrorMessage = error };
}

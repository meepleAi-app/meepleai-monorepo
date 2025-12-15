namespace Api.BoundedContexts.KnowledgeBase.Domain.Indexing;

/// <summary>
/// ADR-016 Phase 3: Domain entity representing an indexed chunk in the vector store.
/// Maps hierarchical chunks (from Phase 1) to Qdrant points with enhanced metadata payload.
/// Entity - identity by Id.
/// </summary>
internal sealed class ChunkIndexEntry
{
    /// <summary>
    /// Unique identifier for this index entry (maps to Qdrant point ID).
    /// </summary>
    public string Id { get; private set; }

    /// <summary>
    /// Reference to the source HierarchicalChunk.
    /// </summary>
    public string ChunkId { get; private set; }

    /// <summary>
    /// Game identifier for filtering.
    /// </summary>
    public Guid GameId { get; private set; }

    /// <summary>
    /// PDF document identifier.
    /// </summary>
    public Guid PdfDocumentId { get; private set; }

    /// <summary>
    /// The embedded text content.
    /// </summary>
    public string Content { get; private set; }

    /// <summary>
    /// The embedding vector (3072 dimensions for text-embedding-3-large).
    /// </summary>
    public float[] Vector { get; private set; }

    /// <summary>
    /// Payload metadata for Qdrant storage and retrieval.
    /// </summary>
    public ChunkPayload Payload { get; private set; }

    /// <summary>
    /// Timestamp when this entry was indexed.
    /// </summary>
    public DateTime IndexedAt { get; private set; }

    /// <summary>
    /// Embedding model used for this vector.
    /// </summary>
    public string EmbeddingModel { get; private set; }

    /// <summary>
    /// Private constructor for factory methods.
    /// </summary>
    private ChunkIndexEntry()
    {
        Id = string.Empty;
        ChunkId = string.Empty;
        Content = string.Empty;
        Vector = [];
        Payload = ChunkPayload.Empty();
        EmbeddingModel = string.Empty;
    }

    /// <summary>
    /// Creates a new chunk index entry from a hierarchical chunk and its embedding.
    /// </summary>
    /// <param name="chunkId">Source chunk ID</param>
    /// <param name="gameId">Game identifier for filtering</param>
    /// <param name="pdfDocumentId">PDF document identifier</param>
    /// <param name="content">Text content</param>
    /// <param name="vector">Embedding vector</param>
    /// <param name="payload">Metadata payload</param>
    /// <param name="embeddingModel">Model used for embedding</param>
    public static ChunkIndexEntry Create(
        string chunkId,
        Guid gameId,
        Guid pdfDocumentId,
        string content,
        float[] vector,
        ChunkPayload payload,
        string embeddingModel = "text-embedding-3-large")
    {
        if (string.IsNullOrWhiteSpace(chunkId))
            throw new ArgumentException("ChunkId cannot be empty", nameof(chunkId));

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be empty", nameof(content));

        if (vector == null || vector.Length == 0)
            throw new ArgumentException("Vector cannot be empty", nameof(vector));

        ArgumentNullException.ThrowIfNull(payload, nameof(payload));

        return new ChunkIndexEntry
        {
            Id = Guid.NewGuid().ToString("N"),
            ChunkId = chunkId,
            GameId = gameId,
            PdfDocumentId = pdfDocumentId,
            Content = content,
            Vector = vector,
            Payload = payload,
            EmbeddingModel = embeddingModel,
            IndexedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a chunk index entry with a specific ID (for reindexing).
    /// </summary>
    public static ChunkIndexEntry CreateWithId(
        string id,
        string chunkId,
        Guid gameId,
        Guid pdfDocumentId,
        string content,
        float[] vector,
        ChunkPayload payload,
        string embeddingModel = "text-embedding-3-large")
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Id cannot be empty", nameof(id));

        var entry = Create(chunkId, gameId, pdfDocumentId, content, vector, payload, embeddingModel);
        entry.Id = id;
        return entry;
    }

    /// <summary>
    /// Updates the vector with a new embedding.
    /// </summary>
    public void UpdateVector(float[] newVector, string model)
    {
        if (newVector == null || newVector.Length == 0)
            throw new ArgumentException("Vector cannot be empty", nameof(newVector));

        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("Model cannot be empty", nameof(model));

        Vector = newVector;
        EmbeddingModel = model;
        IndexedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Checks if this is a parent (section-level) chunk.
    /// </summary>
    public bool IsParent => Payload.ParentChunkId == null;

    /// <summary>
    /// Checks if this chunk has children.
    /// </summary>
    public bool HasChildren => Payload.ChildChunkIds.Count > 0;

    /// <summary>
    /// Gets the vector dimensions.
    /// </summary>
    public int VectorDimensions => Vector.Length;
}

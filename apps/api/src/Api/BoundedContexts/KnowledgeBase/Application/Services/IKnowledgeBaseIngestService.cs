namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Anti-corruption layer (ACL) for ingesting chunks + embeddings from external sources
/// (e.g., DocumentProcessing photo batches) into the KB vector store.
///
/// Preserves BC isolation: external callers do not need access to internal
/// Embedding entity, Vector VO, or IEmbeddingRepository.
///
/// Idempotent: calling twice with same (gameId, sourceDocumentId) results in upsert
/// via VectorDocument lookup pattern.
///
/// Libro Game AI Assistant MVP — Gap G3 ACL.
/// </summary>
internal interface IKnowledgeBaseIngestService
{
    /// <summary>
    /// Persists chunk + embedding pairs to the KB vector store.
    /// Creates a new VectorDocument or reuses existing by (gameId, sourceDocumentId).
    /// Returns count of successfully persisted embeddings.
    /// </summary>
    Task<int> IngestChunksAsync(
        Guid sourceDocumentId,
        Guid gameId,
        IReadOnlyList<ChunkIngestionRequest> requests,
        CancellationToken ct = default);

    /// <summary>
    /// Removes all KB entries (VectorDocument + Embeddings) associated with a source.
    /// Returns number of chunks that were recorded in the removed VectorDocument.
    /// Returns 0 if no VectorDocument exists (graceful no-op).
    /// </summary>
    Task<int> RemoveBySourceAsync(
        Guid sourceDocumentId,
        Guid gameId,
        CancellationToken ct = default);
}

/// <summary>
/// Neutral DTO used by external callers of <see cref="IKnowledgeBaseIngestService"/>.
/// Avoids leaking internal KB domain types (Embedding entity, Vector VO) across BC boundaries.
/// </summary>
internal sealed record ChunkIngestionRequest(
    int PageNumber,
    int ChunkIndex,
    string TextContent,
    float[] Embedding,
    string Language,
    string EmbeddingModel,
    float? OcrConfidence = null);

using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Indexes KnowledgeChunks into the vector store (pgvector) via the KB ACL.
/// Generates embeddings and persists as VectorDocuments in the KB.
/// Libro Game AI Assistant MVP Phase 2 — Task 2.3a / Gap G3.
/// </summary>
internal interface IKnowledgeBaseIndexer
{
    /// <summary>
    /// Indexes all chunks from a completed photo batch.
    /// Emits progress events to caller.
    /// Returns count of indexed chunks.
    /// </summary>
    Task<int> IndexBatchAsync(
        Guid photoBatchUploadId,
        Guid gameId,
        IReadOnlyList<KnowledgeChunk> chunks,
        IProgress<int>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Removes all vector documents for a photo batch (on re-index or deletion).
    /// </summary>
    Task<int> DeleteBatchAsync(Guid photoBatchUploadId, Guid gameId, CancellationToken ct = default);
}

using Api.BoundedContexts.KnowledgeBase.Domain.Chunking;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// ADR-016 Phase 1: Repository interface for hierarchical chunk storage.
/// Implementation deferred to later phases - in-memory implementation for now.
/// </summary>
public interface IChunkRepository
{
    /// <summary>
    /// Gets a chunk by its identifier.
    /// </summary>
    /// <param name="id">The chunk identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The chunk if found, null otherwise.</returns>
    Task<HierarchicalChunk?> GetByIdAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Gets multiple chunks by their identifiers.
    /// </summary>
    /// <param name="ids">The chunk identifiers.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of found chunks.</returns>
    Task<List<HierarchicalChunk>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken ct = default);

    /// <summary>
    /// Gets all child chunks for a parent chunk.
    /// </summary>
    /// <param name="parentId">The parent chunk identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of child chunks.</returns>
    Task<List<HierarchicalChunk>> GetChildrenAsync(string parentId, CancellationToken ct = default);

    /// <summary>
    /// Gets the parent chunk for a child chunk.
    /// </summary>
    /// <param name="childId">The child chunk identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The parent chunk if found, null otherwise.</returns>
    Task<HierarchicalChunk?> GetParentAsync(string childId, CancellationToken ct = default);

    /// <summary>
    /// Saves a single chunk.
    /// </summary>
    /// <param name="chunk">The chunk to save.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SaveAsync(HierarchicalChunk chunk, CancellationToken ct = default);

    /// <summary>
    /// Saves multiple chunks in batch.
    /// </summary>
    /// <param name="chunks">The chunks to save.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SaveBatchAsync(List<HierarchicalChunk> chunks, CancellationToken ct = default);

    /// <summary>
    /// Deletes all chunks for a document.
    /// </summary>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken ct = default);

    /// <summary>
    /// Gets all chunks for a document.
    /// </summary>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of all chunks for the document.</returns>
    Task<List<HierarchicalChunk>> GetByDocumentIdAsync(Guid documentId, CancellationToken ct = default);
}

using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// Adapter interface for vector store operations (currently backed by pgvector).
/// Abstracts vector database details from domain layer.
/// </summary>
internal interface IVectorStoreAdapter
{
    /// <summary>
    /// Performs vector similarity search.
    /// Issue #2051: Supports document filtering via documentIds
    /// </summary>
    Task<List<Embedding>> SearchAsync(
        Guid gameId,
        Vector queryVector,
        int topK,
        double minScore,
        IReadOnlyList<Guid>? documentIds = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs vector similarity search across multiple game IDs.
    /// Used for session-aware RAG where context includes primary game + expansions.
    /// Issue #5580: Session-aware RAG chat.
    /// </summary>
    Task<List<Embedding>> SearchByMultipleGameIdsAsync(
        IReadOnlyList<Guid> gameIds,
        Vector queryVector,
        int topK,
        double minScore,
        IReadOnlyList<Guid>? documentIds = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Indexes a batch of embeddings into the vector store.
    /// </summary>
    Task IndexBatchAsync(
        List<Embedding> embeddings,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all embeddings for a specific vector document.
    /// </summary>
    Task DeleteByVectorDocumentIdAsync(
        Guid vectorDocumentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if vector data exists for the given game.
    /// </summary>
    Task<bool> CollectionExistsAsync(
        Guid gameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures the vector store is ready for the given game.
    /// </summary>
    Task EnsureCollectionExistsAsync(
        Guid gameId,
        int vectorDimension = 768,
        CancellationToken cancellationToken = default);
}

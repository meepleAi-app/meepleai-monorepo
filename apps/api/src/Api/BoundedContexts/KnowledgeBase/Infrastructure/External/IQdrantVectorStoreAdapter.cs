using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// Adapter interface for Qdrant vector database operations.
/// Abstracts Qdrant SDK details from domain layer.
/// </summary>
internal interface IQdrantVectorStoreAdapter
{
    /// <summary>
    /// Performs vector similarity search in Qdrant.
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
    /// Indexes a batch of embeddings into Qdrant.
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
    /// Checks if Qdrant collection exists for the given game.
    /// </summary>
    Task<bool> CollectionExistsAsync(
        Guid gameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new Qdrant collection for a game if it doesn't exist.
    /// </summary>
    Task EnsureCollectionExistsAsync(
        Guid gameId,
        int vectorDimension = 1024,
        CancellationToken cancellationToken = default);
}

using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// Repository interface for Embedding entities.
/// Provides access to embeddings for search and retrieval operations.
/// </summary>
public interface IEmbeddingRepository
{
    /// <summary>
    /// Gets an embedding by its unique identifier.
    /// </summary>
    Task<Embedding?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all embeddings for a specific vector document.
    /// </summary>
    Task<List<Embedding>> GetByVectorDocumentIdAsync(
        Guid vectorDocumentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets embeddings for a specific game (across all documents).
    /// </summary>
    Task<List<Embedding>> GetByGameIdAsync(
        Guid gameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs vector similarity search using the provided query vector.
    /// Issue #2051: Supports document filtering via documentIds
    /// </summary>
    Task<List<Embedding>> SearchByVectorAsync(
        Guid gameId,
        Vector queryVector,
        int topK,
        double minScore,
        IReadOnlyList<Guid>? documentIds = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a batch of embeddings to the repository.
    /// </summary>
    Task AddBatchAsync(
        List<Embedding> embeddings,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all embeddings for a specific vector document.
    /// </summary>
    Task DeleteByVectorDocumentIdAsync(
        Guid vectorDocumentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of embeddings for a specific game.
    /// </summary>
    Task<int> GetCountByGameIdAsync(
        Guid gameId,
        CancellationToken cancellationToken = default);
}

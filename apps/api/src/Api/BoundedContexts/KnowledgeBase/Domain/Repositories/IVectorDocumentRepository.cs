using Api.BoundedContexts.KnowledgeBase.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// Repository interface for VectorDocument aggregate root.
/// Manages persistence of indexed documents in the vector database.
/// </summary>
public interface IVectorDocumentRepository
{
    /// <summary>
    /// Gets a vector document by its unique identifier.
    /// </summary>
    Task<VectorDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a vector document by game ID and source document ID.
    /// </summary>
    Task<VectorDocument?> GetByGameAndSourceAsync(
        Guid gameId,
        Guid sourceDocumentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all vector documents for a specific game.
    /// </summary>
    Task<List<VectorDocument>> GetByGameIdAsync(
        Guid gameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new vector document to the repository.
    /// </summary>
    Task AddAsync(VectorDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing vector document.
    /// </summary>
    Task UpdateAsync(VectorDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a vector document by its ID.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a vector document exists for the given game and source.
    /// </summary>
    Task<bool> ExistsAsync(
        Guid gameId,
        Guid sourceDocumentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of vector documents in the system.
    /// </summary>
    Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of embeddings across all documents.
    /// </summary>
    Task<int> GetTotalEmbeddingsCountAsync(CancellationToken cancellationToken = default);
}

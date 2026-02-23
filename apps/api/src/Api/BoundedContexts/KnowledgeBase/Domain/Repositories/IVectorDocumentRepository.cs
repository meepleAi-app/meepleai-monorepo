using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// Lightweight projection of a VectorDocument's indexing lifecycle state.
/// Returned by IVectorDocumentRepository.GetIndexingInfoByGameIdAsync.
/// </summary>
/// <param name="Status">Typed lifecycle status (compile-time safe).</param>
/// <param name="ChunkCount">Number of indexed chunks.</param>
/// <param name="IndexingError">Error message if status = Failed, otherwise null.</param>
internal record VectorDocumentIndexingInfo(
    VectorDocumentIndexingStatus Status,
    int ChunkCount,
    string? IndexingError);

/// <summary>
/// Repository interface for VectorDocument aggregate root.
/// Manages persistence of indexed documents in the vector database.
/// </summary>
internal interface IVectorDocumentRepository
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

    /// <summary>
    /// Returns lightweight indexing info for a game's VectorDocument (status, chunk count, error).
    /// Returns null if no VectorDocument exists for the game.
    /// Issue #4943: PDF indexing status polling.
    /// </summary>
    Task<VectorDocumentIndexingInfo?> GetIndexingInfoByGameIdAsync(
        Guid gameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if at least one of the given VectorDocument IDs belongs to the specified game.
    /// Used by AgentDefinition validators (Issue #5140) to enforce KB card game ownership.
    /// </summary>
    Task<bool> AnyBelongsToGameAsync(
        IEnumerable<Guid> ids,
        Guid gameId,
        CancellationToken cancellationToken = default);
}

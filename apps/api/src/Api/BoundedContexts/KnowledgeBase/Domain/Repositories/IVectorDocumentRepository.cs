using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// Lightweight projection of a game's indexing lifecycle state (the PDF uploaded last, #4660).
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
    /// Gets all vector documents for a specific game of the shared catalogue.
    /// </summary>
    Task<List<VectorDocument>> GetByGameIdAsync(
        Guid gameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Issue #4244: gets all vector documents of a private game. The caller must have checked that
    /// whoever asks may read that game: this method does not.
    /// </summary>
    Task<List<VectorDocument>> GetByPrivateGameIdAsync(
        Guid privateGameId,
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
    /// Returns lightweight indexing info for a game (status, chunk count, error): the state of the PDF
    /// UPLOADED LAST for the game (#4660), read from the PDF's own <c>ProcessingState</c>; the chunk count and the
    /// error text come from that PDF's vector document, if it has one yet. Returns null if the game has neither a
    /// PDF nor a vector document.
    /// Issue #4943: PDF indexing status polling.
    /// Task 8: parameter is the SharedGame.Id or the private game id (direct resolution, no legacy bridge).
    /// </summary>
    Task<VectorDocumentIndexingInfo?> GetIndexingInfoByGameIdAsync(
        Guid sharedGameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the subset of game IDs that have at least one indexed vector document.
    /// Used by GameSessionOrchestratorService to avoid N+1 queries (Issue #5578).
    /// </summary>
    Task<List<Guid>> GetGameIdsWithDocumentsAsync(
        IEnumerable<Guid> gameIds,
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

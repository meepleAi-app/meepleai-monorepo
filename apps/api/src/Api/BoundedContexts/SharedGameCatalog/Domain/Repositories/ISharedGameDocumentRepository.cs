using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;

/// <summary>
/// Repository interface for SharedGameDocument entities.
/// </summary>
public interface ISharedGameDocumentRepository
{
    /// <summary>
    /// Gets a document by its ID.
    /// </summary>
    Task<SharedGameDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all documents for a shared game.
    /// </summary>
    Task<IReadOnlyList<SharedGameDocument>> GetBySharedGameIdAsync(Guid sharedGameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets documents for a shared game filtered by type.
    /// </summary>
    Task<IReadOnlyList<SharedGameDocument>> GetBySharedGameIdAndTypeAsync(
        Guid sharedGameId,
        SharedGameDocumentType documentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active document for a shared game and type.
    /// </summary>
    Task<SharedGameDocument?> GetActiveDocumentAsync(
        Guid sharedGameId,
        SharedGameDocumentType documentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active documents for a shared game.
    /// </summary>
    Task<IReadOnlyList<SharedGameDocument>> GetActiveDocumentsAsync(
        Guid sharedGameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches documents by tags (for Homerule type).
    /// </summary>
    Task<IReadOnlyList<SharedGameDocument>> SearchByTagsAsync(
        IEnumerable<string> tags,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a version exists for a shared game and type.
    /// </summary>
    Task<bool> VersionExistsAsync(
        Guid sharedGameId,
        SharedGameDocumentType documentType,
        string version,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new document.
    /// </summary>
    Task AddAsync(SharedGameDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing document.
    /// </summary>
    void Update(SharedGameDocument document);

    /// <summary>
    /// Removes a document.
    /// </summary>
    void Remove(SharedGameDocument document);

    /// <summary>
    /// Deactivates all documents of a type for a game (except the specified one).
    /// </summary>
    Task DeactivateOtherVersionsAsync(
        Guid sharedGameId,
        SharedGameDocumentType documentType,
        Guid exceptDocumentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the number of documents created by a specific user.
    /// </summary>
    Task<int> CountByUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

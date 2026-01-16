using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Services;

/// <summary>
/// Domain service for managing document versioning.
/// Ensures only one active version per game + document type.
/// </summary>
public class DocumentVersioningService
{
    private readonly ISharedGameDocumentRepository _repository;

    public DocumentVersioningService(ISharedGameDocumentRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Sets a document as the active version, deactivating all other versions of the same type.
    /// </summary>
    /// <param name="document">The document to activate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public virtual async Task SetActiveVersionAsync(
        SharedGameDocument document,
        CancellationToken cancellationToken = default)
    {
        // Deactivate all other versions of the same type
        await _repository.DeactivateOtherVersionsAsync(
            document.SharedGameId,
            document.DocumentType,
            document.Id,
            cancellationToken).ConfigureAwait(false);

        // Activate this version
        document.SetAsActive();
    }

    /// <summary>
    /// Gets all versions of a document type for a game, ordered by version.
    /// </summary>
    /// <param name="sharedGameId">The shared game ID</param>
    /// <param name="documentType">The document type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of documents ordered by active status and version</returns>
    public virtual async Task<IReadOnlyList<SharedGameDocument>> GetVersionHistoryAsync(
        Guid sharedGameId,
        SharedGameDocumentType documentType,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetBySharedGameIdAndTypeAsync(
            sharedGameId,
            documentType,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the active version of a document type for a game.
    /// </summary>
    /// <param name="sharedGameId">The shared game ID</param>
    /// <param name="documentType">The document type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The active document or null if none is active</returns>
    public virtual async Task<SharedGameDocument?> GetActiveVersionAsync(
        Guid sharedGameId,
        SharedGameDocumentType documentType,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetActiveDocumentAsync(
            sharedGameId,
            documentType,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Validates that a version doesn't already exist for a game + type.
    /// </summary>
    /// <param name="sharedGameId">The shared game ID</param>
    /// <param name="documentType">The document type</param>
    /// <param name="version">The version string</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <exception cref="InvalidOperationException">Thrown when version already exists</exception>
    public virtual async Task ValidateVersionDoesNotExistAsync(
        Guid sharedGameId,
        SharedGameDocumentType documentType,
        string version,
        CancellationToken cancellationToken = default)
    {
        var exists = await _repository.VersionExistsAsync(
            sharedGameId,
            documentType,
            version,
            cancellationToken).ConfigureAwait(false);

        if (exists)
        {
            throw new InvalidOperationException(
                $"Version {version} already exists for {documentType} documents of game {sharedGameId}");
        }
    }
}

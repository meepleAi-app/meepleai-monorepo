using Api.BoundedContexts.DocumentProcessing.Application.DTOs;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// Service interface for managing documents attached to share requests.
/// Issue #2732: Document Attachment Handling for Share Requests.
/// </summary>
public interface IShareRequestDocumentService
{
    /// <summary>
    /// Validates that a user owns the specified document.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="documentId">The ID of the document to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the user owns the document; otherwise false.</returns>
    Task<bool> ValidateDocumentOwnership(
        Guid userId,
        Guid documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that all specified documents exist in the system.
    /// </summary>
    /// <param name="documentIds">List of document IDs to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if all documents exist; otherwise false.</returns>
    Task<bool> ValidateDocumentsExist(
        List<Guid> documentIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attaches documents to a share request.
    /// </summary>
    /// <param name="shareRequestId">The ID of the share request.</param>
    /// <param name="documentIds">List of document IDs to attach.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AttachDocumentsToRequest(
        Guid shareRequestId,
        List<Guid> documentIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the documents attached to a share request (replaces existing attachments).
    /// </summary>
    /// <param name="shareRequestId">The ID of the share request.</param>
    /// <param name="documentIds">List of new document IDs to attach.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateAttachedDocuments(
        Guid shareRequestId,
        List<Guid> documentIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies documents from user library to shared game storage.
    /// Called during share request approval.
    /// </summary>
    /// <param name="sourceDocumentIds">Source document IDs from user library.</param>
    /// <param name="sharedGameId">Target shared game ID.</param>
    /// <param name="contributorId">User ID of the contributor.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of newly created document IDs in shared game storage.</returns>
    Task<List<Guid>> CopyDocumentsToSharedGame(
        List<Guid> sourceDocumentIds,
        Guid sharedGameId,
        Guid contributorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a preview DTO for a document.
    /// </summary>
    /// <param name="documentId">The document ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Document preview data.</returns>
    Task<DocumentPreviewDto> GetDocumentPreview(
        Guid documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets preview DTOs for multiple documents.
    /// </summary>
    /// <param name="documentIds">List of document IDs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of document preview data.</returns>
    Task<List<DocumentPreviewDto>> GetDocumentPreviews(
        List<Guid> documentIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up orphaned document associations when a share request is rejected or withdrawn.
    /// </summary>
    /// <param name="shareRequestId">The share request ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CleanupOrphanedDocuments(
        Guid shareRequestId,
        CancellationToken cancellationToken = default);
}

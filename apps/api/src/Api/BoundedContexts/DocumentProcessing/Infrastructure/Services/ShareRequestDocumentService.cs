using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services.Pdf;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// Implementation of document attachment service for share requests.
/// Issue #2732: Document Attachment Handling.
/// </summary>
internal sealed class ShareRequestDocumentService : IShareRequestDocumentService
{
    private readonly IPdfDocumentRepository _documentRepo;
    private readonly IShareRequestRepository _shareRequestRepo;
    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<ShareRequestDocumentService> _logger;

    public ShareRequestDocumentService(
        IPdfDocumentRepository documentRepo,
        IShareRequestRepository shareRequestRepo,
        IBlobStorageService blobStorage,
        ILogger<ShareRequestDocumentService> logger)
    {
        _documentRepo = documentRepo;
        _shareRequestRepo = shareRequestRepo;
        _blobStorage = blobStorage;
        _logger = logger;
    }

    public async Task<bool> ValidateDocumentOwnership(
        Guid userId,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var document = await _documentRepo.GetByIdAsync(documentId, cancellationToken).ConfigureAwait(false);

        if (document == null)
        {
            _logger.LogWarning("Document {DocumentId} not found for ownership validation", documentId);
            return false;
        }

        var isOwner = document.UploadedByUserId == userId;

        if (!isOwner)
        {
            _logger.LogWarning(
                "User {UserId} does not own document {DocumentId} (Owner: {OwnerId})",
                userId, documentId, document.UploadedByUserId);
        }

        return isOwner;
    }

    public async Task<bool> ValidateDocumentsExist(
        List<Guid> documentIds,
        CancellationToken cancellationToken = default)
    {
        if (documentIds == null || documentIds.Count == 0)
            return true;

        var documents = await _documentRepo.GetByIdsAsync(documentIds, cancellationToken).ConfigureAwait(false);
        var foundCount = documents.Count;
        var allExist = foundCount == documentIds.Count;

        if (!allExist)
        {
            var foundIds = documents.Select(d => d.Id).ToHashSet();
            var missingIds = documentIds.Except(foundIds).ToList();
            _logger.LogWarning(
                "Documents validation failed. Expected: {Expected}, Found: {Found}, Missing: {MissingIds}",
                documentIds.Count, foundCount, string.Join(", ", missingIds));
        }

        return allExist;
    }

    public async Task AttachDocumentsToRequest(
        Guid shareRequestId,
        List<Guid> documentIds,
        CancellationToken cancellationToken = default)
    {
        if (documentIds == null || documentIds.Count == 0)
            return;

        var shareRequest = await _shareRequestRepo.GetByIdForUpdateAsync(shareRequestId, cancellationToken).ConfigureAwait(false);

        if (shareRequest == null)
            throw new NotFoundException("ShareRequest", shareRequestId.ToString());

        // Validate all documents exist before attaching
        var documents = await _documentRepo.GetByIdsAsync(documentIds, cancellationToken).ConfigureAwait(false);

        if (documents.Count != documentIds.Count)
        {
            var foundIds = documents.Select(d => d.Id).ToHashSet();
            var missingIds = documentIds.Except(foundIds).ToList();
            throw new NotFoundException(
                "Document",
                string.Join(", ", missingIds));
        }

        // Attach each document
        foreach (var document in documents)
        {
            shareRequest.AttachDocument(
                document.Id,
                document.FileName.Value,
                document.ContentType,
                document.FileSize.Bytes);
        }

        _shareRequestRepo.Update(shareRequest);

        _logger.LogInformation(
            "Attached {Count} documents to share request {ShareRequestId}",
            documentIds.Count, shareRequestId);
    }

    public async Task UpdateAttachedDocuments(
        Guid shareRequestId,
        List<Guid> documentIds,
        CancellationToken cancellationToken = default)
    {
        var shareRequest = await _shareRequestRepo.GetByIdForUpdateAsync(shareRequestId, cancellationToken).ConfigureAwait(false);

        if (shareRequest == null)
            throw new NotFoundException("ShareRequest", shareRequestId.ToString());

        // Remove all existing attachments
        var currentDocumentIds = shareRequest.AttachedDocuments
            .Select(d => d.DocumentId)
            .ToList();

        foreach (var docId in currentDocumentIds)
        {
            shareRequest.RemoveDocument(docId);
        }

        // Attach new documents
        if (documentIds != null && documentIds.Count > 0)
        {
            var documents = await _documentRepo.GetByIdsAsync(documentIds, cancellationToken).ConfigureAwait(false);

            if (documents.Count != documentIds.Count)
            {
                var foundIds = documents.Select(d => d.Id).ToHashSet();
                var missingIds = documentIds.Except(foundIds).ToList();
                throw new NotFoundException(
                    "Document",
                    string.Join(", ", missingIds));
            }

            foreach (var document in documents)
            {
                shareRequest.AttachDocument(
                    document.Id,
                    document.FileName.Value,
                    document.ContentType,
                    document.FileSize.Bytes);
            }
        }

        _shareRequestRepo.Update(shareRequest);

        _logger.LogInformation(
            "Updated attached documents for share request {ShareRequestId}. New count: {Count}",
            shareRequestId, documentIds?.Count ?? 0);
    }

    public async Task<List<Guid>> CopyDocumentsToSharedGame(
        List<Guid> sourceDocumentIds,
        Guid sharedGameId,
        Guid contributorId,
        CancellationToken cancellationToken = default)
    {
        if (sourceDocumentIds == null || sourceDocumentIds.Count == 0)
            return new List<Guid>();

        var newDocumentIds = new List<Guid>();
        var sourceDocuments = await _documentRepo.GetByIdsAsync(sourceDocumentIds, cancellationToken).ConfigureAwait(false);

        foreach (var sourceDoc in sourceDocuments)
        {
            try
            {
                // #5082: through the blob storage that holds the PDFs, local or S3 per STORAGE_PROVIDER. The copy went
                // through a local-only service that read FilePath as a path on disk: with the PDFs on S3 it found
                // nothing, and in the container it could not write under /app.
                // Both halves of the source key come from the stored path, as in DownloadPdfQueryHandler (#3568).
                var bucketKey = PdfStorageKey.ForPdf(sourceDoc.Id);
                var fileId = PdfStorageKey.FileIdFromPath(sourceDoc.FilePath) ?? bucketKey;
                var resourceKey = PdfStorageKey.ResourceKeyFromPath(sourceDoc.FilePath) ?? bucketKey;

                var source = await _blobStorage
                    .RetrieveAsync(fileId, BlobCategory.Pdf, resourceKey, cancellationToken)
                    .ConfigureAwait(false);
                if (source == null)
                {
                    _logger.LogError(
                        "Document {SourceId} is not in storage at {FilePath}: not copied to shared game {SharedGameId}",
                        sourceDoc.Id, sourceDoc.FilePath, sharedGameId);
                    continue;
                }

                BlobStorageResult stored;
                await using (source.ConfigureAwait(false))
                {
                    // The same place as a PDF an admin uploads for the shared game (UploadSharedGamePdfCommandHandler).
                    stored = await _blobStorage.StoreAsync(
                        source,
                        sourceDoc.FileName.Value,
                        BlobCategory.Pdf,
                        $"shared-game-{sharedGameId}",
                        cancellationToken).ConfigureAwait(false);
                }

                if (!stored.Success || string.IsNullOrEmpty(stored.FilePath))
                {
                    _logger.LogError(
                        "Document {SourceId} could not be stored for shared game {SharedGameId}: {Error}",
                        sourceDoc.Id, sharedGameId, stored.ErrorMessage);
                    continue;
                }

                // Create new document entity for shared game
                var newDocument = PdfDocument.CreateCopy(
                    sourceDoc,
                    sharedGameId,
                    contributorId,
                    stored.FilePath);

                await _documentRepo.AddAsync(newDocument, cancellationToken).ConfigureAwait(false);
                newDocumentIds.Add(newDocument.Id);

                _logger.LogInformation(
                    "Copied document {SourceId} to shared game {SharedGameId} as {NewId}",
                    sourceDoc.Id, sharedGameId, newDocument.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to copy document {SourceId} to shared game {SharedGameId}",
                    sourceDoc.Id, sharedGameId);

                // Continue with next document instead of failing entire operation
            }
        }

        return newDocumentIds;
    }

    public async Task CleanupOrphanedDocuments(
        Guid shareRequestId,
        CancellationToken cancellationToken = default)
    {
        var shareRequest = await _shareRequestRepo.GetByIdForUpdateAsync(shareRequestId, cancellationToken).ConfigureAwait(false);

        if (shareRequest == null)
        {
            _logger.LogWarning(
                "Cannot cleanup documents for non-existent share request {ShareRequestId}",
                shareRequestId);
            return;
        }

        // Documents stay in user's library, just remove associations from share request
        var documentIds = shareRequest.AttachedDocuments
            .Select(d => d.DocumentId)
            .ToList();

        foreach (var docId in documentIds)
        {
            shareRequest.RemoveDocument(docId);
        }

        _shareRequestRepo.Update(shareRequest);

        _logger.LogInformation(
            "Cleaned up {Count} document associations for share request {ShareRequestId}",
            documentIds.Count, shareRequestId);
    }
}
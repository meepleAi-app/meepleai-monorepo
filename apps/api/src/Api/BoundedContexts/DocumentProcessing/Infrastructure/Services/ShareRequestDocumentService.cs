using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure.Services;
using Api.Middleware.Exceptions;
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
    private readonly IStorageService _storageService;
    private readonly ILogger<ShareRequestDocumentService> _logger;

    public ShareRequestDocumentService(
        IPdfDocumentRepository documentRepo,
        IShareRequestRepository shareRequestRepo,
        IStorageService storageService,
        ILogger<ShareRequestDocumentService> logger)
    {
        _documentRepo = documentRepo;
        _shareRequestRepo = shareRequestRepo;
        _storageService = storageService;
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
                // Create storage path for shared game document
                var newStoragePath = $"shared-games/{sharedGameId}/documents/{Guid.NewGuid()}/{sourceDoc.FileName.Value}";

                // Copy file in storage
                var copiedPath = await _storageService.CopyFile(
                    sourceDoc.FilePath,
                    newStoragePath,
                    cancellationToken).ConfigureAwait(false);

                // Create new document entity for shared game
                var newDocument = PdfDocument.CreateCopy(
                    sourceDoc,
                    sharedGameId,
                    contributorId,
                    copiedPath);

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

    public async Task<DocumentPreviewDto> GetDocumentPreview(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var document = await _documentRepo.GetByIdAsync(documentId, cancellationToken).ConfigureAwait(false);

        if (document == null)
            throw new NotFoundException("Document", documentId.ToString());

        var previewUrl = await _storageService.GetPreviewUrl(document.FilePath, cancellationToken).ConfigureAwait(false);

        return new DocumentPreviewDto
        {
            DocumentId = document.Id,
            FileName = document.FileName.Value,
            ContentType = document.ContentType,
            FileSize = document.FileSize.Bytes,
            PreviewUrl = previewUrl,
            PageCount = document.PageCount,
            UploadedAt = document.UploadedAt
        };
    }

    public async Task<List<DocumentPreviewDto>> GetDocumentPreviews(
        List<Guid> documentIds,
        CancellationToken cancellationToken = default)
    {
        if (documentIds == null || documentIds.Count == 0)
            return new List<DocumentPreviewDto>();

        var documents = await _documentRepo.GetByIdsAsync(documentIds, cancellationToken).ConfigureAwait(false);
        var previews = new List<DocumentPreviewDto>();

        foreach (var document in documents)
        {
            try
            {
                var previewUrl = await _storageService.GetPreviewUrl(document.FilePath, cancellationToken).ConfigureAwait(false);

                previews.Add(new DocumentPreviewDto
                {
                    DocumentId = document.Id,
                    FileName = document.FileName.Value,
                    ContentType = document.ContentType,
                    FileSize = document.FileSize.Bytes,
                    PreviewUrl = previewUrl,
                    PageCount = document.PageCount,
                    UploadedAt = document.UploadedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to generate preview for document {DocumentId}",
                    document.Id);

                // Skip this document instead of failing entire operation
            }
        }

        return previews;
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
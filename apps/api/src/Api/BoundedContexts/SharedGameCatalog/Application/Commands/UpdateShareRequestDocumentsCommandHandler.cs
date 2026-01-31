using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for updating the list of documents attached to a share request.
/// Issue #2733: API Endpoints Utente per Share Requests
/// </summary>
internal sealed class UpdateShareRequestDocumentsCommandHandler : ICommandHandler<UpdateShareRequestDocumentsCommand>
{
    private readonly IShareRequestRepository _shareRequestRepository;
    private readonly IPdfDocumentRepository _documentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateShareRequestDocumentsCommandHandler> _logger;

    public UpdateShareRequestDocumentsCommandHandler(
        IShareRequestRepository shareRequestRepository,
        IPdfDocumentRepository documentRepository,
        IUnitOfWork unitOfWork,
        ILogger<UpdateShareRequestDocumentsCommandHandler> logger)
    {
        _shareRequestRepository = shareRequestRepository ?? throw new ArgumentNullException(nameof(shareRequestRepository));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(
        UpdateShareRequestDocumentsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Updating documents for share request {ShareRequestId} by user {UserId}",
            command.ShareRequestId, command.UserId);

        // 1. Get the share request with tracking for updates
        var shareRequest = await _shareRequestRepository.GetByIdForUpdateAsync(
            command.ShareRequestId,
            cancellationToken).ConfigureAwait(false);

        if (shareRequest == null)
        {
            throw new InvalidOperationException($"Share request {command.ShareRequestId} not found");
        }

        // 2. Verify user owns the request
        if (shareRequest.UserId != command.UserId)
        {
            _logger.LogWarning(
                "User {UserId} attempted to update documents for share request {ShareRequestId} owned by {OwnerId}",
                command.UserId, command.ShareRequestId, shareRequest.UserId);
            throw new InvalidOperationException("User does not own this share request");
        }

        // 3. Get current document IDs
        var currentDocumentIds = shareRequest.AttachedDocuments
            .Select(d => d.DocumentId)
            .ToHashSet();

        var newDocumentIds = command.DocumentIds.ToHashSet();

        // 4. Remove documents that are not in the new list
        var documentsToRemove = currentDocumentIds.Except(newDocumentIds).ToList();
        foreach (var docId in documentsToRemove)
        {
            shareRequest.RemoveDocument(docId);
        }

        // 5. Add documents that are new in the list
        var documentsToAdd = newDocumentIds.Except(currentDocumentIds).ToList();
        if (documentsToAdd.Count > 0)
        {
            // Fetch document metadata for validation
            var documents = await _documentRepository.GetByIdsAsync(
                documentsToAdd,
                cancellationToken).ConfigureAwait(false);

            // Verify all documents exist and belong to the user
            foreach (var docId in documentsToAdd)
            {
                var document = documents.FirstOrDefault(d => d.Id == docId);
                if (document == null)
                {
                    throw new InvalidOperationException($"Document {docId} not found");
                }

                if (document.UploadedByUserId != command.UserId)
                {
                    throw new InvalidOperationException($"Document {docId} does not belong to user {command.UserId}");
                }

                shareRequest.AttachDocument(
                    document.Id,
                    document.FileName.Value,
                    document.ContentType,
                    document.FileSize.Bytes);
            }
        }

        // 6. Save changes
        _shareRequestRepository.Update(shareRequest);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Successfully updated documents for share request {ShareRequestId}: {RemovedCount} removed, {AddedCount} added",
            command.ShareRequestId, documentsToRemove.Count, documentsToAdd.Count);
    }
}

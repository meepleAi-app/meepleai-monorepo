using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>
/// Handler for removing a PDF document from a collection.
/// Issue #2051: Remove document from collection
/// </summary>
internal class RemoveDocumentFromCollectionCommandHandler : ICommandHandler<RemoveDocumentFromCollectionCommand, bool>
{
    private readonly IDocumentCollectionRepository _collectionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RemoveDocumentFromCollectionCommandHandler> _logger;

    public RemoveDocumentFromCollectionCommandHandler(
        IDocumentCollectionRepository collectionRepository,
        IUnitOfWork unitOfWork,
        ILogger<RemoveDocumentFromCollectionCommandHandler> logger)
    {
        _collectionRepository = collectionRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<bool> Handle(RemoveDocumentFromCollectionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        _logger.LogInformation(
            "User {UserId} removing document {PdfDocumentId} from collection {CollectionId}",
            command.UserId, command.PdfDocumentId, command.CollectionId);

        // Fetch collection
        var collection = await _collectionRepository.GetByIdAsync(command.CollectionId, cancellationToken).ConfigureAwait(false);
        if (collection == null)
        {
            throw new DomainException($"Collection {command.CollectionId} not found");
        }

        // SECURITY: Verify user owns this collection (same pattern as AddDocumentToCollectionCommandHandler)
        if (collection.CreatedByUserId != command.UserId)
        {
            throw new DomainException($"User {command.UserId} is not authorized to modify collection {command.CollectionId}");
        }

        // Remove document from collection (domain validates document exists)
        collection.RemoveDocument(command.PdfDocumentId);

        // Update collection
        await _collectionRepository.UpdateAsync(collection, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Removed document {PdfDocumentId} from collection {CollectionId} (now {DocumentCount} documents)",
            command.PdfDocumentId, command.CollectionId, collection.DocumentCount);

        return true;
    }
}

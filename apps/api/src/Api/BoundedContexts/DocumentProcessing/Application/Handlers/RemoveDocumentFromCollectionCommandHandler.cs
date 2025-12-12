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
public class RemoveDocumentFromCollectionCommandHandler : ICommandHandler<RemoveDocumentFromCollectionCommand, bool>
{
    private readonly IDocumentCollectionRepository _collectionRepository;
    private readonly IPdfDocumentRepository _pdfRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RemoveDocumentFromCollectionCommandHandler> _logger;

    public RemoveDocumentFromCollectionCommandHandler(
        IDocumentCollectionRepository collectionRepository,
        IPdfDocumentRepository pdfRepository,
        IUnitOfWork unitOfWork,
        ILogger<RemoveDocumentFromCollectionCommandHandler> logger)
    {
        _collectionRepository = collectionRepository;
        _pdfRepository = pdfRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<bool> Handle(RemoveDocumentFromCollectionCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Removing document {PdfDocumentId} from collection {CollectionId}",
            command.PdfDocumentId, command.CollectionId);

        // Fetch collection
        var collection = await _collectionRepository.GetByIdAsync(command.CollectionId, cancellationToken).ConfigureAwait(false);
        if (collection == null)
        {
            throw new DomainException($"Collection {command.CollectionId} not found");
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

    private async Task<DocumentCollectionDto> MapToDto(
        Domain.Entities.DocumentCollection collection,
        CancellationToken cancellationToken)
    {
        var documentDtos = new List<CollectionDocumentDto>();

        foreach (var doc in collection.GetDocumentsOrdered())
        {
            var pdfDoc = await _pdfRepository.GetByIdAsync(doc.PdfDocumentId, cancellationToken).ConfigureAwait(false);

            documentDtos.Add(new CollectionDocumentDto(
                PdfDocumentId: doc.PdfDocumentId,
                DocumentType: doc.Type.Value,
                SortOrder: doc.SortOrder,
                AddedAt: doc.AddedAt,
                PdfDocument: pdfDoc != null ? new PdfDocumentDto(
                    Id: pdfDoc.Id,
                    GameId: pdfDoc.GameId,
                    FileName: pdfDoc.FileName.Value,
                    FilePath: pdfDoc.FilePath,
                    FileSizeBytes: pdfDoc.FileSize.Bytes,
                    ProcessingStatus: pdfDoc.ProcessingStatus,
                    UploadedAt: pdfDoc.UploadedAt,
                    ProcessedAt: pdfDoc.ProcessedAt,
                    PageCount: pdfDoc.PageCount
                ) : null
            ));
        }

        return new DocumentCollectionDto(
            Id: collection.Id,
            GameId: collection.GameId,
            Name: collection.Name.Value,
            Description: collection.Description,
            CreatedByUserId: collection.CreatedByUserId,
            CreatedAt: collection.CreatedAt,
            UpdatedAt: collection.UpdatedAt,
            Documents: documentDtos,
            DocumentCount: collection.DocumentCount,
            IsFull: collection.IsFull
        );
    }
}
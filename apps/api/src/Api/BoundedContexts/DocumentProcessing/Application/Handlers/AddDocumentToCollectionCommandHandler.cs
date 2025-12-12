using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>
/// Handler for adding a PDF document to an existing collection.
/// Issue #2051: Add document with validation (max 5 docs, no duplicates)
/// </summary>
public class AddDocumentToCollectionCommandHandler : ICommandHandler<AddDocumentToCollectionCommand, bool>
{
    private readonly IDocumentCollectionRepository _collectionRepository;
    private readonly IPdfDocumentRepository _pdfRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AddDocumentToCollectionCommandHandler> _logger;

    public AddDocumentToCollectionCommandHandler(
        IDocumentCollectionRepository collectionRepository,
        IPdfDocumentRepository pdfRepository,
        IUnitOfWork unitOfWork,
        ILogger<AddDocumentToCollectionCommandHandler> logger)
    {
        _collectionRepository = collectionRepository;
        _pdfRepository = pdfRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<bool> Handle(AddDocumentToCollectionCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Adding document {PdfDocumentId} to collection {CollectionId}",
            command.PdfDocumentId, command.CollectionId);

        // Fetch collection
        var collection = await _collectionRepository.GetByIdAsync(command.CollectionId, cancellationToken).ConfigureAwait(false);
        if (collection == null)
        {
            throw new DomainException($"Collection {command.CollectionId} not found");
        }

        // Validate PDF document exists
        var pdfDoc = await _pdfRepository.GetByIdAsync(command.PdfDocumentId, cancellationToken).ConfigureAwait(false);
        if (pdfDoc == null)
        {
            throw new DomainException($"PDF document {command.PdfDocumentId} not found");
        }

        // Validate PDF belongs to same game as collection
        if (pdfDoc.GameId != collection.GameId)
        {
            throw new DomainException(
                $"PDF document {command.PdfDocumentId} (game {pdfDoc.GameId}) does not belong to collection's game {collection.GameId}");
        }

        // Parse and validate document type
        var documentType = new DocumentType(command.DocumentType);

        // Add document to collection (domain validates max 5 documents, no duplicates)
        collection.AddDocument(command.PdfDocumentId, documentType, command.SortOrder);

        // Update collection
        await _collectionRepository.UpdateAsync(collection, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Added document {PdfDocumentId} to collection {CollectionId} (now {DocumentCount} documents)",
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
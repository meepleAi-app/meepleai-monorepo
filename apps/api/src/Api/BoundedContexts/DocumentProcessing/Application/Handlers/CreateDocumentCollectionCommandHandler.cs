using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>
/// Handler for creating a new document collection with initial documents.
/// Issue #2051: Multi-document collection creation with validation
/// </summary>
internal class CreateDocumentCollectionCommandHandler : ICommandHandler<CreateDocumentCollectionCommand, DocumentCollectionDto>
{
    private readonly IDocumentCollectionRepository _collectionRepository;
    private readonly IPdfDocumentRepository _pdfRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateDocumentCollectionCommandHandler> _logger;

    public CreateDocumentCollectionCommandHandler(
        IDocumentCollectionRepository collectionRepository,
        IPdfDocumentRepository pdfRepository,
        IUnitOfWork unitOfWork,
        ILogger<CreateDocumentCollectionCommandHandler> logger)
    {
        _collectionRepository = collectionRepository ?? throw new ArgumentNullException(nameof(collectionRepository));
        _pdfRepository = pdfRepository ?? throw new ArgumentNullException(nameof(pdfRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DocumentCollectionDto> Handle(CreateDocumentCollectionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        _logger.LogInformation(
            "Creating document collection for game {GameId} by user {UserId}",
            command.GameId, command.UserId);

        // Validate: only one collection per game
        var existingCollection = await _collectionRepository.FindByGameIdAsync(command.GameId, cancellationToken).ConfigureAwait(false);
        if (existingCollection != null)
        {
            throw new DomainException($"Game {command.GameId} already has a document collection");
        }

        // Validate collection name
        var collectionName = new CollectionName(command.Name);

        // Create collection aggregate
        var collection = new DocumentCollection(
            Guid.NewGuid(),
            command.GameId,
            collectionName,
            command.UserId,
            command.Description);

        // Add initial documents if provided
        foreach (var docRequest in command.InitialDocuments)
        {
            // Validate PDF document exists
            var pdfDoc = await _pdfRepository.GetByIdAsync(docRequest.PdfDocumentId, cancellationToken).ConfigureAwait(false);
            if (pdfDoc == null)
            {
                throw new DomainException($"PDF document {docRequest.PdfDocumentId} not found");
            }

            // Validate PDF belongs to same game
            if (pdfDoc.GameId != command.GameId)
            {
                throw new DomainException($"PDF document {docRequest.PdfDocumentId} does not belong to game {command.GameId}");
            }

            // Parse and validate document type
            var documentType = new DocumentType(docRequest.DocumentType);

            // Add document to collection (validates max 5 documents, no duplicates)
            collection.AddDocument(docRequest.PdfDocumentId, documentType, docRequest.SortOrder);
        }

        // Save collection
        await _collectionRepository.AddAsync(collection, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Created document collection {CollectionId} for game {GameId} with {DocumentCount} documents",
            collection.Id, command.GameId, collection.DocumentCount);

        return await MapToDto(collection, cancellationToken).ConfigureAwait(false);
    }

    private async Task<DocumentCollectionDto> MapToDto(DocumentCollection collection, CancellationToken cancellationToken)
    {
        // PERF-05: Batch query to avoid N+1 problem
        var pdfIds = collection.Documents.Select(d => d.PdfDocumentId).ToList();
        var pdfDocs = await _pdfRepository.GetByIdsAsync(pdfIds, cancellationToken).ConfigureAwait(false);
        var pdfDict = pdfDocs.ToDictionary(p => p.Id);

        var documentDtos = new List<CollectionDocumentDto>();

        foreach (var doc in collection.GetDocumentsOrdered())
        {
            var pdfDoc = pdfDict.GetValueOrDefault(doc.PdfDocumentId);

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

                    UploadedAt: pdfDoc.UploadedAt,
                    ProcessedAt: pdfDoc.ProcessedAt,
                    PageCount: pdfDoc.PageCount,
                    ProcessingState: pdfDoc.ProcessingState.ToString(),
                    ProgressPercentage: pdfDoc.ProgressPercentage,
                    RetryCount: pdfDoc.RetryCount,
                    MaxRetries: pdfDoc.MaxRetries,
                    DocumentCategory: pdfDoc.DocumentCategory.ToString(),
                    BaseDocumentId: pdfDoc.BaseDocumentId,
                    IsActiveForRag: pdfDoc.IsActiveForRag,
                    HasAcceptedDisclaimer: pdfDoc.HasAcceptedDisclaimer,
                    VersionLabel: pdfDoc.VersionLabel
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

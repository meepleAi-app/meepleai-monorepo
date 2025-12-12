using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>
/// Handler for retrieving document collection by collection ID.
/// Issue #2051: Get collection with full document details
/// </summary>
public class GetCollectionByIdQueryHandler : IQueryHandler<GetCollectionByIdQuery, DocumentCollectionDto?>
{
    private readonly IDocumentCollectionRepository _collectionRepository;
    private readonly IPdfDocumentRepository _pdfRepository;
    private readonly ILogger<GetCollectionByIdQueryHandler> _logger;

    public GetCollectionByIdQueryHandler(
        IDocumentCollectionRepository collectionRepository,
        IPdfDocumentRepository pdfRepository,
        ILogger<GetCollectionByIdQueryHandler> logger)
    {
        _collectionRepository = collectionRepository ?? throw new ArgumentNullException(nameof(collectionRepository));
        _pdfRepository = pdfRepository ?? throw new ArgumentNullException(nameof(pdfRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DocumentCollectionDto?> Handle(GetCollectionByIdQuery query, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving document collection {CollectionId}", query.CollectionId);

        var collection = await _collectionRepository.GetByIdAsync(query.CollectionId, cancellationToken).ConfigureAwait(false);

        if (collection == null)
        {
            _logger.LogDebug("Collection {CollectionId} not found", query.CollectionId);
            return null;
        }

        return await MapToDto(collection, cancellationToken).ConfigureAwait(false);
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
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>
/// Handler for retrieving document collection by game ID.
/// Issue #2051: Get collection with full document details
/// </summary>
internal class GetCollectionByGameQueryHandler : IQueryHandler<GetCollectionByGameQuery, DocumentCollectionDto?>
{
    private readonly IDocumentCollectionRepository _collectionRepository;
    private readonly IPdfDocumentRepository _pdfRepository;
    private readonly ILogger<GetCollectionByGameQueryHandler> _logger;

    public GetCollectionByGameQueryHandler(
        IDocumentCollectionRepository collectionRepository,
        IPdfDocumentRepository pdfRepository,
        ILogger<GetCollectionByGameQueryHandler> logger)
    {
        _collectionRepository = collectionRepository ?? throw new ArgumentNullException(nameof(collectionRepository));
        _pdfRepository = pdfRepository ?? throw new ArgumentNullException(nameof(pdfRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DocumentCollectionDto?> Handle(GetCollectionByGameQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        _logger.LogInformation("Retrieving document collection for game {GameId}", query.GameId);

        var collection = await _collectionRepository.FindByGameIdAsync(query.GameId, cancellationToken).ConfigureAwait(false);

        if (collection == null)
        {
            _logger.LogDebug("No collection found for game {GameId}", query.GameId);
            return null;
        }

        return await MapToDto(collection, cancellationToken).ConfigureAwait(false);
    }

    private async Task<DocumentCollectionDto> MapToDto(
        Domain.Entities.DocumentCollection collection,
        CancellationToken cancellationToken)
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
                    ProcessingStatus: pdfDoc.ProcessingStatus,
                    UploadedAt: pdfDoc.UploadedAt,
                    ProcessedAt: pdfDoc.ProcessedAt,
                    PageCount: pdfDoc.PageCount,
                    ProcessingState: pdfDoc.ProcessingState.ToString(),
                    ProgressPercentage: pdfDoc.ProgressPercentage,
                    RetryCount: pdfDoc.RetryCount,
                    MaxRetries: pdfDoc.MaxRetries,
                    DocumentCategory: pdfDoc.DocumentCategory.ToString(),
                    BaseDocumentId: pdfDoc.BaseDocumentId
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

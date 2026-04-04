using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Handler for retrieving all document collections created by a specific user.
/// Issue #2051: Get collections by user ID
/// </summary>
internal class GetCollectionsByUserQueryHandler : IQueryHandler<GetCollectionsByUserQuery, IReadOnlyList<DocumentCollectionDto>>
{
    private readonly IDocumentCollectionRepository _collectionRepository;
    private readonly IPdfDocumentRepository _pdfRepository;
    private readonly ILogger<GetCollectionsByUserQueryHandler> _logger;

    public GetCollectionsByUserQueryHandler(
        IDocumentCollectionRepository collectionRepository,
        IPdfDocumentRepository pdfRepository,
        ILogger<GetCollectionsByUserQueryHandler> logger)
    {
        _collectionRepository = collectionRepository ?? throw new ArgumentNullException(nameof(collectionRepository));
        _pdfRepository = pdfRepository ?? throw new ArgumentNullException(nameof(pdfRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<DocumentCollectionDto>> Handle(GetCollectionsByUserQuery query, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving document collections for user {UserId}", query.UserId);

        var collections = await _collectionRepository.FindByUserIdAsync(query.UserId, cancellationToken).ConfigureAwait(false);

        var collectionDtos = new List<DocumentCollectionDto>();
        foreach (var collection in collections)
        {
            collectionDtos.Add(await MapToDto(collection, cancellationToken).ConfigureAwait(false));
        }

        _logger.LogDebug("Found {Count} collections for user {UserId}", collectionDtos.Count, query.UserId);

        return collectionDtos;
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

using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Handlers;

internal class GetPdfDocumentByIdQueryHandler : IQueryHandler<GetPdfDocumentByIdQuery, PdfDocumentDto?>
{
    private readonly IPdfDocumentRepository _documentRepository;

    public GetPdfDocumentByIdQueryHandler(IPdfDocumentRepository documentRepository)
    {
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
    }

    public async Task<PdfDocumentDto?> Handle(GetPdfDocumentByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var document = await _documentRepository.GetByIdAsync(query.DocumentId, cancellationToken).ConfigureAwait(false);

        return document != null ? MapToDto(document) : null;
    }

    private static PdfDocumentDto MapToDto(PdfDocument doc)
    {
        return new PdfDocumentDto(
            Id: doc.Id,
            GameId: doc.GameId,
            FileName: doc.FileName.Value,
            FilePath: doc.FilePath,
            FileSizeBytes: doc.FileSize.Bytes,
            ProcessingStatus: doc.ProcessingStatus,
            UploadedAt: doc.UploadedAt,
            ProcessedAt: doc.ProcessedAt,
            PageCount: doc.PageCount,
            ProcessingState: doc.ProcessingState.ToString(),
            ProgressPercentage: doc.ProgressPercentage,
            RetryCount: doc.RetryCount,
            MaxRetries: doc.MaxRetries,
            CanRetry: doc.CanRetry(),
            ErrorCategory: doc.ErrorCategory?.ToString(),
            ProcessingError: doc.ProcessingError,
            DocumentCategory: doc.DocumentCategory.ToString()
        );
    }
}

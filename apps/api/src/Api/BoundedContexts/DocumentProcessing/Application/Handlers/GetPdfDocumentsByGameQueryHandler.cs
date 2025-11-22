using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Handlers;

public class GetPdfDocumentsByGameQueryHandler : IQueryHandler<GetPdfDocumentsByGameQuery, IReadOnlyList<PdfDocumentDto>>
{
    private readonly IPdfDocumentRepository _documentRepository;

    public GetPdfDocumentsByGameQueryHandler(IPdfDocumentRepository documentRepository)
    {
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
    }

    public async Task<IReadOnlyList<PdfDocumentDto>> Handle(GetPdfDocumentsByGameQuery query, CancellationToken cancellationToken)
    {
        var documents = await _documentRepository.FindByGameIdAsync(query.GameId, cancellationToken);

        return documents.Select(MapToDto).ToList();
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
            PageCount: doc.PageCount
        );
    }
}

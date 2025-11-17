using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>
/// Handler for GetPdfOwnershipQuery.
/// Uses repository to retrieve ownership and status from domain entity.
/// SEC-02: Row-Level Security for PDF operations
/// </summary>
public class GetPdfOwnershipQueryHandler : IQueryHandler<GetPdfOwnershipQuery, PdfOwnershipResult?>
{
    private readonly IPdfDocumentRepository _documentRepository;
    private readonly ILogger<GetPdfOwnershipQueryHandler> _logger;

    public GetPdfOwnershipQueryHandler(
        IPdfDocumentRepository documentRepository,
        ILogger<GetPdfOwnershipQueryHandler> logger)
    {
        _documentRepository = documentRepository;
        _logger = logger;
    }

    public async Task<PdfOwnershipResult?> Handle(GetPdfOwnershipQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var document = await _documentRepository.GetByIdAsync(query.PdfId, cancellationToken);

            if (document == null)
            {
                _logger.LogWarning("PDF {PdfId} not found for ownership query", query.PdfId);
                return null;
            }

            return new PdfOwnershipResult(
                Id: document.Id,
                UploadedByUserId: document.UploadedByUserId,
                GameId: document.GameId,
                ProcessingStatus: document.ProcessingStatus
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving PDF ownership for {PdfId}", query.PdfId);
            return null;
        }
    }
}

using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>
/// Handler for GetPdfTextQuery.
/// Retrieves extracted text from PDF document.
/// NOTE: Uses DbContext directly for ExtractedText field (not in domain entity).
/// TODO: Consider adding ExtractedText to domain entity or create read model.
/// </summary>
public class GetPdfTextQueryHandler : IQueryHandler<GetPdfTextQuery, PdfTextResult?>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetPdfTextQueryHandler> _logger;

    public GetPdfTextQueryHandler(
        MeepleAiDbContext dbContext,
        ILogger<GetPdfTextQueryHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PdfTextResult?> Handle(GetPdfTextQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var pdf = await _dbContext.PdfDocuments
                .Where(p => p.Id == query.PdfId)
                .AsNoTracking()
                .Select(p => new PdfTextResult(
                    p.Id,
                    p.FileName,
                    p.ExtractedText,
                    p.ProcessingStatus,
                    p.ProcessedAt,
                    p.PageCount,
                    p.CharacterCount,
                    p.ProcessingError
                ))
                .FirstOrDefaultAsync(cancellationToken);

            if (pdf == null)
            {
                _logger.LogWarning("PDF {PdfId} not found", query.PdfId);
            }

            return pdf;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving PDF text for {PdfId}", query.PdfId);
            return null;
        }
    }
}

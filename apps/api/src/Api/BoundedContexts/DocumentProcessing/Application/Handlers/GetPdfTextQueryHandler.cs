using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>
/// Handler for GetPdfTextQuery.
/// Retrieves extracted text from PDF document.
/// NOTE: Uses DbContext directly for ExtractedText field (not in domain entity).
/// </summary>
internal class GetPdfTextQueryHandler : IQueryHandler<GetPdfTextQuery, PdfTextResult?>
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
        ArgumentNullException.ThrowIfNull(query);
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
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            if (pdf == null)
            {
                _logger.LogWarning("PDF {PdfId} not found", query.PdfId);
            }

            return pdf;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: QUERY HANDLER PATTERN - CQRS query boundary
        // Generic catch handles unexpected infrastructure failures (DB, network)
        // to prevent exception propagation to API layer. Returns null on failure.
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving PDF text for {PdfId}", query.PdfId);
            return null;
        }
#pragma warning restore CA1031
    }
}

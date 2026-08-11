using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

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
                .Select(p => new
                {
                    p.Id,
                    p.FileName,
                    p.ExtractedText,
                    p.ProcessingState,
                    p.ProcessedAt,
                    p.PageCount,
                    p.CharacterCount,
                    p.ProcessingError,
                    p.SharedGameId,
                    p.UploadedByUserId
                })
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            if (pdf == null)
            {
                _logger.LogWarning("PDF {PdfId} not found", query.PdfId);
                return null;
            }

            // Issue #3222 — Authorization: shared-game PDFs are public (citation viewer); private
            // PDFs require ownership; admins bypass (the admin-queue extracted-text tool reads any
            // PDF). Return null (→ 404, no info leak) otherwise, so a non-owner cannot read another
            // user's private rulebook text.
            var isSharedGamePdf = pdf.SharedGameId.HasValue;
            var isOwner = pdf.UploadedByUserId == query.UserId;
            if (!query.IsAdmin && !isSharedGamePdf && !isOwner)
            {
                _logger.LogWarning(
                    "User {UserId} denied access to PDF text for {PdfId} (owner: {OwnerId})",
                    query.UserId, query.PdfId, pdf.UploadedByUserId);
                return null;
            }

            return new PdfTextResult(
                pdf.Id,
                pdf.FileName,
                pdf.ExtractedText,
                pdf.ProcessingState,
                pdf.ProcessedAt,
                pdf.PageCount,
                pdf.CharacterCount,
                pdf.ProcessingError);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // QUERY HANDLER PATTERN: CQRS query boundary
        // Generic catch handles unexpected infrastructure failures (DB, network)
        // to prevent exception propagation to API layer. Returns null on failure.
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving PDF text for {PdfId}", query.PdfId);
            return null;
        }
#pragma warning restore CA1031
    }
}

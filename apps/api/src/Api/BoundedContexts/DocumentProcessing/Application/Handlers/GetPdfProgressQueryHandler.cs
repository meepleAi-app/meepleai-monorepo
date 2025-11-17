using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>
/// Handler for GetPdfProgressQuery.
/// Retrieves PDF processing progress including ownership.
/// NOTE: Uses DbContext directly for ProcessingProgressJson field (not in domain entity).
/// TODO: Consider adding ProcessingProgress to domain entity as value object.
/// PDF-08: Get PDF processing progress
/// </summary>
public class GetPdfProgressQueryHandler : IQueryHandler<GetPdfProgressQuery, PdfProgressResult?>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetPdfProgressQueryHandler> _logger;

    public GetPdfProgressQueryHandler(
        MeepleAiDbContext dbContext,
        ILogger<GetPdfProgressQueryHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<PdfProgressResult?> Handle(GetPdfProgressQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var pdf = await _dbContext.PdfDocuments
                .Where(p => p.Id == query.PdfId)
                .AsNoTracking()
                .Select(p => new PdfProgressResult(
                    p.Id,
                    p.UploadedByUserId,
                    p.ProcessingProgressJson
                ))
                .FirstOrDefaultAsync(cancellationToken);

            if (pdf == null)
            {
                _logger.LogWarning("PDF {PdfId} not found for progress query", query.PdfId);
            }

            return pdf;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving PDF progress for {PdfId}", query.PdfId);
            return null;
        }
    }
}

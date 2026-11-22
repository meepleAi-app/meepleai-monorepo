using System.Text.Json;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.Infrastructure;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Handler for GetPdfProgressQuery.
/// Retrieves PDF processing progress including ownership.
/// NOTE: Uses DbContext directly for ProcessingProgressJson field (not in domain entity).
/// PDF-08: Get PDF processing progress
/// </summary>
internal class GetPdfProgressQueryHandler : IQueryHandler<GetPdfProgressQuery, PdfProgressResult?>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetPdfProgressQueryHandler> _logger;

    public GetPdfProgressQueryHandler(
        MeepleAiDbContext dbContext,
        ILogger<GetPdfProgressQueryHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PdfProgressResult?> Handle(GetPdfProgressQuery query, CancellationToken cancellationToken)
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
                    p.UploadedByUserId,
                    p.ProcessingProgressJson,
                    p.ProcessingState,
                    p.ProcessingError,
                    p.ProcessedAt,
                    p.UploadedAt,
                })
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            if (pdf == null)
            {
                _logger.LogWarning("PDF {PdfId} not found for progress query", query.PdfId);
                return null;
            }

            var progress = ReadRecordedProgress(pdf.Id, pdf.ProcessingProgressJson)
                ?? PdfProgressFromState.Derive(pdf.ProcessingState, pdf.ProcessingError, pdf.ProcessedAt, pdf.UploadedAt);

            return new PdfProgressResult(pdf.Id, pdf.UploadedByUserId, progress);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // QUERY HANDLER PATTERN: CQRS query boundary
        // Generic catch handles unexpected infrastructure failures (DB, network)
        // to prevent exception propagation to API layer. Returns null on failure.
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving PDF progress for {PdfId}", query.PdfId);
            return null;
        }
#pragma warning restore CA1031
    }

    /// <summary>The progress the pipeline recorded, deserialized as the endpoint used to (default options).</summary>
    private ProcessingProgress? ReadRecordedProgress(Guid pdfId, string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ProcessingProgress>(json);
        }
#pragma warning disable CA1031 // A corrupt progress falls back to the one derived from the state
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize progress for PDF {PdfId}", pdfId);
            return null;
        }
#pragma warning restore CA1031
    }
}

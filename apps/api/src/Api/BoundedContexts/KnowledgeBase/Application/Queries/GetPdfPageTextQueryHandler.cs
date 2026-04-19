using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Returns the extracted text for a specific page of a PDF document.
/// Text is reconstructed from TextChunk entities ordered by ChunkIndex.
/// </summary>
internal sealed class GetPdfPageTextQueryHandler : IRequestHandler<GetPdfPageTextQuery, PdfPageTextDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetPdfPageTextQueryHandler> _logger;

    public GetPdfPageTextQueryHandler(
        MeepleAiDbContext dbContext,
        ILogger<GetPdfPageTextQueryHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PdfPageTextDto> Handle(GetPdfPageTextQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Find the PdfDocument
        var pdfDocument = await _dbContext.PdfDocuments
            .AsNoTracking()
            .Where(p => p.Id == request.PdfId)
            .Select(p => new { p.Id, p.FileName, p.PageCount })
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (pdfDocument is null)
        {
            throw new NotFoundException("PdfDocument", request.PdfId.ToString());
        }

        // 2. Query TextChunks for this PDF
        var allChunks = await _dbContext.TextChunks
            .AsNoTracking()
            .Where(tc => tc.PdfDocumentId == request.PdfId && tc.PageNumber != null)
            .Select(tc => new { tc.PageNumber, tc.ChunkIndex, tc.Content })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        // 3. Get total distinct page count from chunks (or fall back to PdfDocument.PageCount)
        var totalPages = allChunks
            .Select(c => c.PageNumber!.Value)
            .Distinct()
            .Count();

        if (totalPages == 0 && pdfDocument.PageCount.HasValue)
        {
            totalPages = pdfDocument.PageCount.Value;
        }

        // 4. Filter chunks for the requested page, ordered by ChunkIndex
        var pageChunks = allChunks
            .Where(c => c.PageNumber == request.PageNumber)
            .OrderBy(c => c.ChunkIndex)
            .Select(c => c.Content)
            .ToList();

        if (pageChunks.Count == 0)
        {
            _logger.LogWarning(
                "No text chunks found for PDF {PdfId} page {PageNumber}",
                request.PdfId, request.PageNumber);
            throw new NotFoundException("PdfPage", $"{request.PdfId}/page/{request.PageNumber}");
        }

        // 5. Join chunk texts
        var pageText = string.Join("\n\n", pageChunks);

        // 6. Return DTO
        return new PdfPageTextDto(
            PageNumber: request.PageNumber,
            Text: pageText,
            DocumentTitle: pdfDocument.FileName,
            TotalPages: totalPages
        );
    }
}

using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

internal class GetPdfImageRegionsQueryHandler
    : IQueryHandler<GetPdfImageRegionsQuery, IReadOnlyList<ImageRegionDto>>
{
    private readonly MeepleAiDbContext _dbContext;

    public GetPdfImageRegionsQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IReadOnlyList<ImageRegionDto>> Handle(
        GetPdfImageRegionsQuery query, CancellationToken cancellationToken)
    {
        // Owner-or-shared-game scoping (mirror GetPdfTextQueryHandler #3222): shared-game PDFs are
        // public (citation viewer); private PDFs require ownership; admins bypass. A missing or
        // unauthorized PDF returns an empty list — indistinguishable from a region-less PDF, so it
        // leaks neither existence nor another user's table/figure layout.
        var pdf = await _dbContext.PdfDocuments
            .Where(p => p.Id == query.PdfId)
            .AsNoTracking()
            .Select(p => new { p.SharedGameId, p.UploadedByUserId })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (pdf is null)
        {
            return Array.Empty<ImageRegionDto>();
        }

        var isSharedGamePdf = pdf.SharedGameId.HasValue;
        var isOwner = pdf.UploadedByUserId == query.UserId;
        if (!query.IsAdmin && !isSharedGamePdf && !isOwner)
        {
            return Array.Empty<ImageRegionDto>();
        }

        var regions = await _dbContext.PdfImageRegions
            .Where(r => r.PdfDocumentId == query.PdfId)
            .OrderBy(r => r.PageNumber)
            .AsNoTracking()
            .Select(r => new ImageRegionDto(r.PageNumber, r.X, r.Y, r.Width, r.Height, r.ElementType))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return regions;
    }
}

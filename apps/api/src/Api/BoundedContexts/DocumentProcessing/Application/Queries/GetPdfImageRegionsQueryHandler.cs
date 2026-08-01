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

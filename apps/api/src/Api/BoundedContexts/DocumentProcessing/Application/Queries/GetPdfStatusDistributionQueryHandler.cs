using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Handler for GetPdfStatusDistributionQuery.
/// PDF Storage Management Hub: Phase 6 analytics.
/// </summary>
internal sealed class GetPdfStatusDistributionQueryHandler
    : IQueryHandler<GetPdfStatusDistributionQuery, PdfStatusDistributionDto>
{
    private readonly MeepleAiDbContext _dbContext;

    public GetPdfStatusDistributionQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<PdfStatusDistributionDto> Handle(
        GetPdfStatusDistributionQuery request, CancellationToken cancellationToken)
    {
        var countByState = await _dbContext.PdfDocuments
            .AsNoTracking()
            .GroupBy(p => p.ProcessingState)
            .Select(g => new { State = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.State, x => x.Count, cancellationToken)
            .ConfigureAwait(false);

        var totalDocuments = countByState.Values.Sum();

        var topBySize = await _dbContext.PdfDocuments
            .AsNoTracking()
            .OrderByDescending(p => p.FileSizeBytes)
            .Take(5)
            .Select(p => new PdfSizeRankItem(p.Id, p.FileName, p.FileSizeBytes))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PdfStatusDistributionDto(countByState, totalDocuments, topBySize);
    }
}

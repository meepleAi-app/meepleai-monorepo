using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// #3435 SP2 router (DC-B): selects the table-heavy PDFs whose image-region count reaches the
/// candidate threshold. Read-only scoped selector reusing the SP1 eligibility filter (Ready,
/// in-corpus, non-demo); the candidacy rule itself lives in <see cref="TableRegionCandidateDecider"/>
/// and is applied here as a <c>HAVING count(*) &gt;= threshold</c>.
/// </summary>
internal sealed class GetTableRegionCandidatesQueryHandler
    : IQueryHandler<GetTableRegionCandidatesQuery, IReadOnlyList<TableRegionCandidateDto>>
{
    internal const string MinImageRegionsConfigKey = "PdfProcessing:TableRegionRouter:MinImageRegions";
    /// <summary>Defensive cap when the caller passes no limit (corpus is ~100s of PDFs; keeps payload bounded).</summary>
    internal const int DefaultLimit = 200;

    private readonly MeepleAiDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public GetTableRegionCandidatesQueryHandler(MeepleAiDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task<IReadOnlyList<TableRegionCandidateDto>> Handle(
        GetTableRegionCandidatesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Resolve the threshold (query override → config → MVP default), then normalize THROUGH the
        // decider so the SQL HAVING predicate below stays in lockstep with IsCandidate — the clamp
        // (<1 → 1) is defined once in TableRegionCandidateDecider, not re-implemented here.
        var minImageRegions = TableRegionCandidateDecider.NormalizeThreshold(
            query.MinImageRegions
                ?? _configuration.GetValue<int?>(MinImageRegionsConfigKey)
                ?? TableRegionCandidateDecider.DefaultMinImageRegions);

        var limit = query.Limit is > 0 ? query.Limit.Value : DefaultLimit;

        var readyState = nameof(PdfProcessingState.Ready);
        var demoPrefix = PdfDocumentEntity.DemoMockFilePathPrefix;

        // Eligibility mirrors the SP1 seed selector (Ready, in-corpus via IndexerVersion, non-demo) MINUS
        // the seed marker: the router's signal is the region COUNT itself (spec §5quinquies DC-B),
        // regardless of whether the regions came from the SP1 batch or the manual admin path (#3447) — so
        // it does NOT filter on ImageRegionsSeededAt. A PDF with zero regions never appears in the group-by.
        var eligiblePdfIds = _dbContext.PdfDocuments
            .AsNoTracking()
            .Where(p => p.ProcessingState == readyState
                && p.IndexerVersion != null
                && !p.FilePath.StartsWith(demoPrefix))
            .Select(p => p.Id);

        // HAVING count(*) >= threshold, most-dense first. Project to an anonymous type in SQL, then map
        // to the positional record in memory (records can trip EF's group-by projection translation).
        var rows = await _dbContext.PdfImageRegions
            .AsNoTracking()
            .Where(r => eligiblePdfIds.Contains(r.PdfDocumentId))
            .GroupBy(r => r.PdfDocumentId)
            .Where(g => g.Count() >= minImageRegions)
            .Select(g => new
            {
                PdfId = g.Key,
                Count = g.Count(),
                Pages = g.Select(r => r.PageNumber).Distinct().Count(),
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.PdfId)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(x => new TableRegionCandidateDto(x.PdfId, x.Count, x.Pages))
            .ToList();
    }
}

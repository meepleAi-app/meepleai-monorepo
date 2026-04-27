using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicExtractor;

/// <summary>
/// Handler for <see cref="GetMechanicAnalysisClaimsQuery"/> (ISSUE-584).
/// Projects every claim of an analysis (with its citations) into the read DTOs consumed by the
/// admin claims-review UI. Bypasses <c>IsSuppressed</c> filters so moderators can still inspect
/// suppressed analyses while triaging.
/// </summary>
internal sealed class GetMechanicAnalysisClaimsQueryHandler
    : IQueryHandler<GetMechanicAnalysisClaimsQuery, IReadOnlyList<MechanicClaimDto>?>
{
    private readonly MeepleAiDbContext _dbContext;

    public GetMechanicAnalysisClaimsQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IReadOnlyList<MechanicClaimDto>?> Handle(
        GetMechanicAnalysisClaimsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var analysisExists = await _dbContext.MechanicAnalyses
            .AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(a => a.Id == request.AnalysisId, cancellationToken)
            .ConfigureAwait(false);

        if (!analysisExists)
        {
            return null;
        }

        var claims = await _dbContext.MechanicClaims
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(c => c.Citations)
            .Where(c => c.AnalysisId == request.AnalysisId)
            .OrderBy(c => c.Section)
            .ThenBy(c => c.DisplayOrder)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return claims
            .Select(c => new MechanicClaimDto(
                Id: c.Id,
                AnalysisId: c.AnalysisId,
                Section: (MechanicSection)c.Section,
                Text: c.Text,
                DisplayOrder: c.DisplayOrder,
                Status: (MechanicClaimStatus)c.Status,
                ReviewedBy: c.ReviewedBy,
                ReviewedAt: c.ReviewedAt,
                RejectionNote: c.RejectionNote,
                Citations: c.Citations
                    .OrderBy(citation => citation.DisplayOrder)
                    .Select(citation => new MechanicCitationDto(
                        Id: citation.Id,
                        PdfPage: citation.PdfPage,
                        Quote: citation.Quote,
                        DisplayOrder: citation.DisplayOrder))
                    .ToList()))
            .ToList();
    }
}

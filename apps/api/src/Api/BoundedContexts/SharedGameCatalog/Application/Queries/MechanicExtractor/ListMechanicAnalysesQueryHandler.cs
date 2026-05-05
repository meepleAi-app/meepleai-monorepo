using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicExtractor;

/// <summary>
/// Handler for <see cref="ListMechanicAnalysesQuery"/> — spec-panel gap #2.
/// Streams a page of analyses ordered by <c>CreatedAt DESC</c>, joining the shared game
/// catalog for the display title and projecting the claims count from a correlated subquery
/// so the SQL stays a single round-trip.
/// </summary>
/// <remarks>
/// <para>
/// Bypasses the <c>IsSuppressed</c> global query filter via <c>IgnoreQueryFilters()</c> so
/// suppressed rows remain reachable from the discovery list. The same trade-off is taken by
/// <see cref="GetMechanicAnalysisStatusQueryHandler"/>: suppression is orthogonal to the
/// review lifecycle (ADR-051 T5) and admins must be able to inspect a suppressed analysis
/// without losing the entry point.
/// </para>
/// <para>
/// The total-count query also ignores filters so the pagination header stays consistent
/// with the items page (otherwise a suppressed row visible on page N could disappear from
/// the count, breaking the "page N of M" affordance).
/// </para>
/// </remarks>
internal sealed class ListMechanicAnalysesQueryHandler
    : IQueryHandler<ListMechanicAnalysesQuery, MechanicAnalysisListPageDto>
{
    private readonly MeepleAiDbContext _dbContext;

    public ListMechanicAnalysesQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<MechanicAnalysisListPageDto> Handle(
        ListMechanicAnalysesQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1
            ? 1
            : Math.Min(request.PageSize, ListMechanicAnalysesQuery.MaxPageSize);

        var baseQuery = _dbContext.MechanicAnalyses
            .AsNoTracking()
            .IgnoreQueryFilters();

        var totalCount = await baseQuery
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        // LEFT JOIN on SharedGames so we still surface a row even if the catalog row was
        // soft-deleted out from under the analysis (orphan analysis defensive read).
        // Correlated subquery on MechanicClaims for ClaimsCount: cheap because the FK is
        // indexed and projection-only saves us a JOIN explosion.
        var items = await (
            from a in baseQuery
            join g in _dbContext.SharedGames.AsNoTracking().IgnoreQueryFilters()
                on a.SharedGameId equals g.Id into gs
            from g in gs.DefaultIfEmpty()
            orderby a.CreatedAt descending
            select new MechanicAnalysisListItemDto(
                a.Id,
                a.SharedGameId,
                g != null ? g.Title : "(unknown game)",
                a.PdfDocumentId,
                a.PromptVersion,
                (MechanicAnalysisStatus)a.Status,
                _dbContext.MechanicClaims
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Count(c => c.AnalysisId == a.Id),
                a.TotalTokensUsed,
                a.EstimatedCostUsd,
                a.CertificationStatus,
                a.IsSuppressed,
                a.CreatedAt))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new MechanicAnalysisListPageDto(
            Items: items,
            Page: page,
            PageSize: pageSize,
            TotalCount: totalCount);
    }
}

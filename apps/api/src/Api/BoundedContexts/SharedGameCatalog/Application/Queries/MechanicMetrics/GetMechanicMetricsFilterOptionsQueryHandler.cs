using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;

using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicMetrics;

internal sealed class GetMechanicMetricsFilterOptionsQueryHandler
    : IQueryHandler<GetMechanicMetricsFilterOptionsQuery, MechanicMetricsFilterOptionsDto>
{
    private readonly MeepleAiDbContext _db;

    public GetMechanicMetricsFilterOptionsQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<MechanicMetricsFilterOptionsDto> Handle(
        GetMechanicMetricsFilterOptionsQuery request, CancellationToken cancellationToken)
    {
        // Games that have at least one (non-suppressed) analysis. The MechanicAnalyses query filter
        // excludes suppressed rows; the join to SharedGames also drops soft-deleted games. DISTINCT on
        // an anonymous {Id, Title} tuple (EF can't translate Distinct over a custom record); map after.
        var gameRows = await (
            from a in _db.MechanicAnalyses.AsNoTracking()
            join g in _db.SharedGames on a.SharedGameId equals g.Id
            select new { g.Id, g.Title })
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var games = gameRows
            .Select(x => new MechanicFilterOptionDto(x.Id, x.Title))
            .OrderBy(o => o.Name, StringComparer.Ordinal)
            .ToList();

        // Reviewers that have reviewed at least one analysis.
        var reviewerRows = await (
            from a in _db.MechanicAnalyses.AsNoTracking()
            where a.ReviewedBy != null
            join u in _db.Users on a.ReviewedBy!.Value equals u.Id
            select new { u.Id, Name = u.DisplayName ?? u.Email })
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var reviewers = reviewerRows
            .Select(x => new MechanicFilterOptionDto(x.Id, x.Name))
            .OrderBy(o => o.Name, StringComparer.Ordinal)
            .ToList();

        return new MechanicMetricsFilterOptionsDto(games, reviewers);
    }
}

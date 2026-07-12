using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;

using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicMetrics;

internal sealed class GetMechanicRecentAnalysesQueryHandler
    : IQueryHandler<GetMechanicRecentAnalysesQuery, MechanicRecentAnalysesResult>
{
    private const int MaxLimit = 200;

    private readonly MeepleAiDbContext _db;

    public GetMechanicRecentAnalysesQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<MechanicRecentAnalysesResult> Handle(
        GetMechanicRecentAnalysesQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var limit = Math.Clamp(request.Limit, 1, MaxLimit);
        var offset = Math.Max(0, request.Offset);

        var query = _db.MechanicAnalyses.AsNoTracking().AsQueryable();
        if (request.GameId is Guid gameId)
        {
            query = query.Where(a => a.SharedGameId == gameId);
        }
        if (request.ReviewerId is Guid reviewerId)
        {
            query = query.Where(a => a.ReviewedBy == reviewerId);
        }
        if (request.Status is int status)
        {
            query = query.Where(a => a.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .Select(a => new MechanicRecentAnalysisRowDto(
                a.Id,
                a.SharedGameId,
                _db.SharedGames.Where(g => g.Id == a.SharedGameId).Select(g => g.Title).FirstOrDefault() ?? "—",
                a.Status,
                a.ReviewedBy,
                a.ReviewedBy == null
                    ? null
                    : _db.Users.Where(u => u.Id == a.ReviewedBy).Select(u => u.DisplayName ?? u.Email).FirstOrDefault(),
                a.CreatedAt,
                a.ReviewedAt,
                a.EstimatedCostUsd))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new MechanicRecentAnalysesResult(items, totalCount);
    }
}

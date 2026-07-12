using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;

using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicMetrics;

internal sealed class GetMechanicCostByDayQueryHandler
    : IQueryHandler<GetMechanicCostByDayQuery, IReadOnlyList<MechanicCostByDayDto>>
{
    private const int MaxDays = 90;
    private const int MinDays = 1;

    private readonly MeepleAiDbContext _db;
    private readonly TimeProvider _timeProvider;

    public GetMechanicCostByDayQueryHandler(MeepleAiDbContext db, TimeProvider timeProvider)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<IReadOnlyList<MechanicCostByDayDto>> Handle(
        GetMechanicCostByDayQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var days = Math.Clamp(request.Days, MinDays, MaxDays);

        var todayUtc = _timeProvider.GetUtcNow().UtcDateTime.Date;
        var startDate = todayUtc.AddDays(-(days - 1));

        var query = _db.MechanicAnalyses.AsNoTracking().Where(a => a.CreatedAt >= startDate);
        if (request.GameId is Guid gameId)
        {
            query = query.Where(a => a.SharedGameId == gameId);
        }
        if (request.ReviewerId is Guid reviewerId)
        {
            query = query.Where(a => a.ReviewedBy == reviewerId);
        }

        var byDay = await query
            .GroupBy(a => a.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Cost = g.Sum(a => a.EstimatedCostUsd), Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Gap-fill: emit one bucket per day so the chart has a continuous x-axis.
        var result = new List<MechanicCostByDayDto>(days);
        for (var i = 0; i < days; i++)
        {
            var day = startDate.AddDays(i);
            var bucket = byDay.FirstOrDefault(b => b.Date == day);
            result.Add(new MechanicCostByDayDto(
                DateOnly.FromDateTime(day),
                bucket?.Cost ?? 0m,
                bucket?.Count ?? 0));
        }

        return result;
    }
}

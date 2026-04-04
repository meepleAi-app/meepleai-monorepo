using Api.BoundedContexts.Administration.Application.DTOs;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries.ServiceCalls;

internal sealed class GetServiceCallSummaryQueryHandler
    : IQueryHandler<GetServiceCallSummaryQuery, IReadOnlyList<ServiceCallSummaryDto>>
{
    private readonly MeepleAiDbContext _db;

    public GetServiceCallSummaryQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<IReadOnlyList<ServiceCallSummaryDto>> Handle(
        GetServiceCallSummaryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var since = ParsePeriod(query.Period);

        // Push aggregation to database — avoids loading all rows into memory
        var groups = await _db.ServiceCallLogs
            .AsNoTracking()
            .Where(x => x.TimestampUtc >= since)
            .GroupBy(x => x.ServiceName)
            .Select(g => new
            {
                ServiceName = g.Key,
                TotalCalls = g.Count(),
                SuccessCount = g.Count(x => x.IsSuccess),
                ErrorCount = g.Count(x => !x.IsSuccess),
                AvgLatencyMs = g.Average(x => (double)x.LatencyMs),
                MaxLatencyMs = g.Max(x => x.LatencyMs),
                LastCallAt = g.Max(x => (DateTime?)x.TimestampUtc),
                LastErrorAt = g.Where(x => !x.IsSuccess).Max(x => (DateTime?)x.TimestampUtc),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var result = groups
            .Select(g =>
            {
                var errorRate = g.TotalCalls > 0 ? (double)g.ErrorCount / g.TotalCalls : 0.0;
                // P95 approximation: use MaxLatencyMs. Exact P95 requires raw SQL percentile_cont.
                var p95Latency = (double)g.MaxLatencyMs;

                return new ServiceCallSummaryDto(
                    ServiceName: g.ServiceName,
                    TotalCalls: g.TotalCalls,
                    SuccessCount: g.SuccessCount,
                    ErrorCount: g.ErrorCount,
                    ErrorRate: errorRate,
                    AvgLatencyMs: g.AvgLatencyMs,
                    P95LatencyMs: p95Latency,
                    MaxLatencyMs: g.MaxLatencyMs,
                    LastCallAt: g.LastCallAt,
                    LastErrorAt: g.LastErrorAt);
            })
            .OrderByDescending(x => x.TotalCalls)
            .ToList();

        return result;
    }

    private static DateTime ParsePeriod(string? period)
    {
        return period?.ToLowerInvariant() switch
        {
            "1h" => DateTime.UtcNow.AddHours(-1),
            "6h" => DateTime.UtcNow.AddHours(-6),
            "7d" => DateTime.UtcNow.AddDays(-7),
            _ => DateTime.UtcNow.AddHours(-24) // default: 24h
        };
    }

}

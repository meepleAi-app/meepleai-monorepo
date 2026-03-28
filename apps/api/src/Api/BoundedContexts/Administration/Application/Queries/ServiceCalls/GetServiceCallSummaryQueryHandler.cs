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

        var entries = await _db.ServiceCallLogs
            .AsNoTracking()
            .Where(x => x.TimestampUtc >= since)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var grouped = entries
            .GroupBy(x => x.ServiceName, StringComparer.Ordinal)
            .Select(g =>
            {
                var all = g.ToList();
                var successCount = all.Count(x => x.IsSuccess);
                var errorCount = all.Count(x => !x.IsSuccess);
                var totalCalls = all.Count;
                var errorRate = totalCalls > 0 ? (double)errorCount / totalCalls : 0.0;
                var avgLatency = totalCalls > 0 ? all.Average(x => x.LatencyMs) : 0.0;
                var maxLatency = totalCalls > 0 ? all.Max(x => x.LatencyMs) : 0L;
                var p95Latency = CalculateP95(all.Select(x => x.LatencyMs).ToList());
                var lastCallAt = all.Count > 0 ? all.Max(x => (DateTime?)x.TimestampUtc) : null;
                var lastErrorAt = all.Any(x => !x.IsSuccess)
                    ? all.Where(x => !x.IsSuccess).Max(x => (DateTime?)x.TimestampUtc)
                    : null;

                return new ServiceCallSummaryDto(
                    ServiceName: g.Key,
                    TotalCalls: totalCalls,
                    SuccessCount: successCount,
                    ErrorCount: errorCount,
                    ErrorRate: errorRate,
                    AvgLatencyMs: avgLatency,
                    P95LatencyMs: p95Latency,
                    MaxLatencyMs: maxLatency,
                    LastCallAt: lastCallAt,
                    LastErrorAt: lastErrorAt);
            })
            .OrderByDescending(x => x.TotalCalls)
            .ToList();

        return grouped;
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

    private static double CalculateP95(List<long> latencies)
    {
        if (latencies.Count == 0) return 0.0;
        latencies.Sort();
        var index = (int)Math.Ceiling(0.95 * latencies.Count) - 1;
        index = Math.Max(0, Math.Min(index, latencies.Count - 1));
        return latencies[index];
    }
}

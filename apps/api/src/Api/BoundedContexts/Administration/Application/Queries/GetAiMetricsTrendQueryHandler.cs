using Api.BoundedContexts.Administration.Application.DTOs;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries;

internal sealed class GetAiMetricsTrendQueryHandler
    : IQueryHandler<GetAiMetricsTrendQuery, AiMetricsTrendResult>
{
    private readonly MeepleAiDbContext _db;
    private readonly TimeProvider _timeProvider;

    public GetAiMetricsTrendQueryHandler(MeepleAiDbContext db, TimeProvider? timeProvider = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<AiMetricsTrendResult> Handle(GetAiMetricsTrendQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var config = ResolveRangeOrThrow(request.Range);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var windowStart = AlignToBucket(now - config.Window, config.Bucket);
        var windowEnd = AlignToBucket(now, config.Bucket) + config.Bucket; // exclusive

        // Load only the latency + status + bucket for the window. AsNoTracking
        // since this is read-only analytics. For the volumes we expect today
        // (≤ a few thousand rows per range) the in-memory percentile is
        // cheaper to implement correctly than translating percentile_cont
        // through EF Core; if the table grows past ~100k/range we'll lift
        // the calculation into SQL.
        var rows = await _db.AiRequestLogs
            .AsNoTracking()
            .Where(log => log.CreatedAt >= windowStart && log.CreatedAt < windowEnd)
            .Select(log => new RowProjection
            {
                CreatedAt = log.CreatedAt,
                LatencyMs = log.LatencyMs,
                Status = log.Status,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var grouped = rows
            .GroupBy(r => AlignToBucket(r.CreatedAt, config.Bucket))
            .ToDictionary(g => g.Key, g => g.ToList());

        var datapoints = new List<AiMetricsDatapoint>();
        for (var ts = windowStart; ts < windowEnd; ts += config.Bucket)
        {
            datapoints.Add(BuildDatapoint(ts, grouped.GetValueOrDefault(ts)));
        }

        return new AiMetricsTrendResult
        {
            Range = request.Range,
            BucketSize = config.Label,
            Datapoints = datapoints,
        };
    }

    private static AiMetricsDatapoint BuildDatapoint(DateTime timestamp, IReadOnlyList<RowProjection>? bucket)
    {
        if (bucket is null || bucket.Count == 0)
        {
            return new AiMetricsDatapoint
            {
                Timestamp = timestamp,
                RequestCount = 0,
                AvgLatencyMs = 0,
                P50LatencyMs = 0,
                P95LatencyMs = 0,
                ErrorRate = 0,
            };
        }

        var latencies = bucket.Select(r => r.LatencyMs).OrderBy(x => x).ToList();
        var errors = bucket.Count(r => !string.Equals(r.Status, "Success", StringComparison.OrdinalIgnoreCase));

        return new AiMetricsDatapoint
        {
            Timestamp = timestamp,
            RequestCount = bucket.Count,
            AvgLatencyMs = (int)Math.Round(latencies.Average()),
            P50LatencyMs = Percentile(latencies, 0.50),
            P95LatencyMs = Percentile(latencies, 0.95),
            ErrorRate = (double)errors / bucket.Count,
        };
    }

    private static int Percentile(IReadOnlyList<int> sorted, double q)
    {
        // Sorted ascending. Nearest-rank percentile keeps the integer
        // semantics — close enough to percentile_cont for chart axes
        // and trivially testable.
        if (sorted.Count == 0) return 0;
        var rank = (int)Math.Ceiling(q * sorted.Count);
        if (rank <= 0) rank = 1;
        if (rank > sorted.Count) rank = sorted.Count;
        return sorted[rank - 1];
    }

    private static DateTime AlignToBucket(DateTime timestamp, TimeSpan bucket)
    {
        var ticks = timestamp.Ticks - (timestamp.Ticks % bucket.Ticks);
        return new DateTime(ticks, DateTimeKind.Utc);
    }

    private static RangeConfig ResolveRangeOrThrow(string range)
    {
        return range switch
        {
            "Live" or "1h" => new RangeConfig(TimeSpan.FromHours(1), TimeSpan.FromMinutes(1), "1m"),
            "24h" => new RangeConfig(TimeSpan.FromHours(24), TimeSpan.FromMinutes(15), "15m"),
            "7d" => new RangeConfig(TimeSpan.FromDays(7), TimeSpan.FromHours(1), "1h"),
            _ => throw new ArgumentException(
                $"Unsupported range '{range}'. Valid values: Live, 1h, 24h, 7d.",
                nameof(range)),
        };
    }

    private sealed record RowProjection
    {
        public required DateTime CreatedAt { get; init; }
        public required int LatencyMs { get; init; }
        public required string Status { get; init; }
    }

    private sealed record RangeConfig(TimeSpan Window, TimeSpan Bucket, string Label);
}

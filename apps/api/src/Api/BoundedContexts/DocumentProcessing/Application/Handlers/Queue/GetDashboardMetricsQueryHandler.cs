using Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Handlers.Queue;

/// <summary>
/// Returns aggregated processing metrics for the admin dashboard.
/// Issue #5459: Per-phase timing + metrics dashboard.
/// </summary>
internal sealed class GetDashboardMetricsQueryHandler
    : IQueryHandler<GetDashboardMetricsQuery, DashboardMetricsDto>
{
    private readonly MeepleAiDbContext _db;

    public GetDashboardMetricsQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<DashboardMetricsDto> Handle(
        GetDashboardMetricsQuery query,
        CancellationToken cancellationToken)
    {
        var cutoff = GetCutoff(query.Period);

        // Phase timings from ProcessingMetricEntity
        var metrics = await _db.Set<ProcessingMetricEntity>()
            .Where(m => m.CreatedAt >= cutoff)
            .GroupBy(m => m.Step)
            .Select(g => new PhaseTimingDto(
                g.Key,
                g.Average(m => (double)m.DurationSeconds),
                g.Min(m => (double)m.DurationSeconds),
                g.Max(m => (double)m.DurationSeconds),
                g.Count()))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Failure rate from PdfDocumentEntity
        var docs = _db.Set<PdfDocumentEntity>()
            .Where(d => d.UploadedAt >= cutoff);

        var totalProcessed = await docs
            .CountAsync(d => d.ProcessingStatus == "completed", cancellationToken)
            .ConfigureAwait(false);

        var totalFailed = await docs
            .CountAsync(d => d.ProcessingStatus == "failed", cancellationToken)
            .ConfigureAwait(false);

        var total = totalProcessed + totalFailed;
        var failureRate = total > 0 ? (double)totalFailed / total * 100 : 0;

        // Average total duration across all steps per document
        var avgTotal = metrics.Count > 0
            ? metrics.Sum(m => m.AvgDurationSeconds)
            : 0;

        return new DashboardMetricsDto(
            PhaseTimings: metrics,
            TotalProcessed: totalProcessed,
            TotalFailed: totalFailed,
            FailureRatePercent: Math.Round(failureRate, 1),
            AvgTotalDurationSeconds: Math.Round(avgTotal, 1),
            Period: query.Period);
    }

    private static DateTime GetCutoff(string period) => period switch
    {
        "7d" => DateTime.UtcNow.AddDays(-7),
        "30d" => DateTime.UtcNow.AddDays(-30),
        _ => DateTime.UtcNow.AddHours(-24), // Default: 24h
    };
}

using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;
using Api.Infrastructure;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Handlers.Queue;

/// <summary>
/// Returns currently active alerts for the processing queue.
/// Issue #5460: Proactive alerts — on-demand check for dashboard.
/// </summary>
internal sealed class GetActiveAlertsQueryHandler
    : IQueryHandler<GetActiveAlertsQuery, IReadOnlyList<QueueAlertDto>>
{
    private readonly MeepleAiDbContext _db;
    private readonly IConfiguration _configuration;

    public GetActiveAlertsQueryHandler(MeepleAiDbContext db, IConfiguration configuration)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task<IReadOnlyList<QueueAlertDto>> Handle(
        GetActiveAlertsQuery query,
        CancellationToken cancellationToken)
    {
        var alerts = new List<QueueAlertDto>();
        var now = DateTimeOffset.UtcNow;

        // Check 1: Stuck documents
        var stuckTimeout = TimeSpan.FromMinutes(
            _configuration.GetValue("ProcessingQueueMonitor:StuckJobTimeoutMinutes", 10));
        var stuckCutoff = now - stuckTimeout;

        var stuckJobs = await _db.Set<ProcessingJobEntity>()
            .Where(j => j.Status == "Processing" && j.StartedAt != null && j.StartedAt < stuckCutoff)
            .Select(j => new { j.Id, j.PdfDocumentId, FileName = j.PdfDocument.FileName, j.StartedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var job in stuckJobs)
        {
            var stuckMinutes = Math.Round((now - job.StartedAt!.Value).TotalMinutes, 1);
            alerts.Add(new QueueAlertDto(
                QueueAlertType.DocumentStuck,
                stuckMinutes > 30 ? QueueAlertSeverity.Critical : QueueAlertSeverity.Warning,
                $"Document '{job.FileName}' stuck in processing for {stuckMinutes} minutes",
                now,
                new StuckDocumentAlertData(job.Id, job.PdfDocumentId, job.FileName, stuckMinutes)));
        }

        // Check 2: Queue depth
        var depthThreshold = _configuration.GetValue("ProcessingQueueMonitor:QueueDepthThreshold", 20);
        var pendingCount = await _db.Set<ProcessingJobEntity>()
            .CountAsync(j => j.Status == "Queued", cancellationToken)
            .ConfigureAwait(false);

        if (pendingCount > depthThreshold)
        {
            alerts.Add(new QueueAlertDto(
                QueueAlertType.QueueDepthHigh,
                pendingCount > depthThreshold * 2 ? QueueAlertSeverity.Critical : QueueAlertSeverity.Warning,
                $"Queue depth ({pendingCount}) exceeds threshold ({depthThreshold})",
                now,
                new QueueDepthAlertData(pendingCount, depthThreshold)));
        }

        // Check 3: Failure rate (last hour)
        var failureThreshold = _configuration.GetValue("ProcessingQueueMonitor:FailureRateThresholdPercent", 15.0);
        var oneHourAgo = now.AddHours(-1);

        var completedCount = await _db.Set<ProcessingJobEntity>()
            .CountAsync(j => j.Status == "Completed" && j.CompletedAt >= oneHourAgo, cancellationToken)
            .ConfigureAwait(false);

        var failedCount = await _db.Set<ProcessingJobEntity>()
            .CountAsync(j => j.Status == "Failed" && j.CompletedAt >= oneHourAgo, cancellationToken)
            .ConfigureAwait(false);

        var total = completedCount + failedCount;
        if (total > 0)
        {
            var failureRate = Math.Round((double)failedCount / total * 100, 1);
            if (failureRate > failureThreshold)
            {
                alerts.Add(new QueueAlertDto(
                    QueueAlertType.HighFailureRate,
                    failureRate > 30 ? QueueAlertSeverity.Critical : QueueAlertSeverity.Warning,
                    $"Failure rate {failureRate}% ({failedCount}/{total}) exceeds threshold ({failureThreshold}%)",
                    now,
                    new HighFailureRateAlertData(failureRate, failureThreshold, failedCount, total)));
            }
        }

        return alerts;
    }
}

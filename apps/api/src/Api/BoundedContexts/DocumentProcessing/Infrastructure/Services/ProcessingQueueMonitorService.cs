using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.Infrastructure;
using Api.Infrastructure.Entities.DocumentProcessing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// Background service that monitors the processing queue for anomalies.
/// Issue #5460: Proactive alerts (stuck docs, high failure, queue depth).
/// Checks every 2 minutes for:
/// - Documents stuck in Processing state > 10 minutes
/// - Queue depth exceeding threshold (default: 20)
/// - Failure rate > 15% in the last hour (sliding window)
/// </summary>
#pragma warning disable CA1031 // Background service boundary — catch general exceptions
internal sealed class ProcessingQueueMonitorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProcessingQueueMonitorService> _logger;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _timeProvider;
    private readonly DateTimeOffset _serviceStartedAt;

    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(30);

    public ProcessingQueueMonitorService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<ProcessingQueueMonitorService> logger,
        TimeProvider? timeProvider = null)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _serviceStartedAt = _timeProvider.GetUtcNow();
    }

    private TimeSpan CheckInterval =>
        TimeSpan.FromSeconds(_configuration.GetValue("ProcessingQueueMonitor:CheckIntervalSeconds", 120));

    private TimeSpan StuckJobTimeout =>
        TimeSpan.FromMinutes(_configuration.GetValue("ProcessingQueueMonitor:StuckJobTimeoutMinutes", 10));

    // Recovery threshold — higher than the alert threshold (10 min) so the early "stuck"
    // warning fires first, but a merely-slow job is never degraded. #2689.
    private TimeSpan StuckJobRecoveryTimeout =>
        TimeSpan.FromMinutes(_configuration.GetValue("ProcessingQueueMonitor:StuckJobRecoveryTimeoutMinutes", 30));

    // Startup grace window — auto-degrade is suppressed while the service is younger than
    // this value, allowing StalePdfRecoveryService to complete its one-shot startup pass
    // before the monitor starts degrading the same jobs. #2693.
    private TimeSpan StartupGracePeriod =>
        TimeSpan.FromMinutes(_configuration.GetValue("ProcessingQueueMonitor:StartupGracePeriodMinutes", 15));

    private bool StartupGraceElapsed() => (_timeProvider.GetUtcNow() - _serviceStartedAt) >= StartupGracePeriod;

    private int QueueDepthThreshold =>
        _configuration.GetValue("ProcessingQueueMonitor:QueueDepthThreshold", 20);

    private double FailureRateThreshold =>
        _configuration.GetValue("ProcessingQueueMonitor:FailureRateThresholdPercent", 15.0);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ProcessingQueueMonitorService starting with interval {Interval}s",
            CheckInterval.TotalSeconds);

        await Task.Delay(InitialDelay, stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunChecksAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during queue monitoring check");
            }

            await Task.Delay(CheckInterval, stoppingToken).ConfigureAwait(false);
        }

        _logger.LogInformation("ProcessingQueueMonitorService stopped");
    }

    // Exposed as internal for unit testing (InternalsVisibleTo Api.Tests). Issue #2689.
    internal async Task RunChecksAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var streamService = scope.ServiceProvider.GetRequiredService<IQueueStreamService>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await CheckStuckJobsAsync(db, streamService, mediator, ct).ConfigureAwait(false);
        await CheckQueueDepthAsync(db, streamService, ct).ConfigureAwait(false);
        await CheckFailureRateAsync(db, streamService, ct).ConfigureAwait(false);
    }

    private async Task CheckStuckJobsAsync(
        MeepleAiDbContext db,
        IQueueStreamService streamService,
        IMediator mediator,
        CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow - StuckJobTimeout;

        var stuckJobs = await db.Set<ProcessingJobEntity>()
            .Where(j => j.Status == "Processing" && j.StartedAt != null && j.StartedAt < cutoff)
            .Select(j => new
            {
                j.Id,
                j.PdfDocumentId,
                FileName = j.PdfDocument.FileName,
                j.StartedAt,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var graceElapsed = StartupGraceElapsed();

        foreach (var job in stuckJobs)
        {
            var stuckMinutes = (DateTimeOffset.UtcNow - job.StartedAt!.Value).TotalMinutes;

            _logger.LogWarning(
                "Stuck document detected: Job {JobId} (PDF: {FileName}) stuck for {Minutes:F1} minutes",
                job.Id, job.FileName, stuckMinutes);

            var evt = new QueueStreamEvent(
                QueueStreamEventType.AlertDocumentStuck,
                job.Id,
                new StuckDocumentAlertData(job.Id, job.PdfDocumentId, job.FileName, Math.Round(stuckMinutes, 1)),
                DateTimeOffset.UtcNow);

            await streamService.PublishQueueEventAsync(evt, ct).ConfigureAwait(false);

            // Past recovery threshold → auto-degrade via command (Issue #2689).
            // Degrade-only: the monitor sends the command; all state mutation lives in
            // DegradeStuckJobCommandHandler. Does NOT re-queue (that was reverted in #2686).
            // Gated behind startup grace (Issue #2693): suppressed while the service is younger
            // than StartupGracePeriod to avoid racing StalePdfRecoveryService on first cycle.
            if (stuckMinutes >= StuckJobRecoveryTimeout.TotalMinutes)
            {
                if (!graceElapsed)
                {
                    _logger.LogDebug(
                        "Skipping auto-degrade for job {JobId} (stuck {Minutes:F1} min): startup grace period has not elapsed (Issue #2693)",
                        job.Id, stuckMinutes);
                    continue;
                }

                var result = await mediator.Send(new DegradeStuckJobCommand(job.Id, stuckMinutes), ct).ConfigureAwait(false);
                if (result.Degraded)
                {
                    _logger.LogWarning(
                        "Auto-degraded stuck job {JobId} (PDF: {FileName}) to Failed after {Minutes:F1} min (Issue #2689)",
                        job.Id, job.FileName, stuckMinutes);
                }
            }
        }
    }

    private async Task CheckQueueDepthAsync(
        MeepleAiDbContext db,
        IQueueStreamService streamService,
        CancellationToken ct)
    {
        var pendingCount = await db.Set<ProcessingJobEntity>()
            .CountAsync(j => j.Status == "Queued", ct)
            .ConfigureAwait(false);

        if (pendingCount > QueueDepthThreshold)
        {
            _logger.LogWarning(
                "Queue depth alert: {Count} pending jobs exceeds threshold of {Threshold}",
                pendingCount, QueueDepthThreshold);

            var evt = new QueueStreamEvent(
                QueueStreamEventType.AlertQueueDepthHigh,
                Guid.Empty,
                new QueueDepthAlertData(pendingCount, QueueDepthThreshold),
                DateTimeOffset.UtcNow);

            await streamService.PublishQueueEventAsync(evt, ct).ConfigureAwait(false);
        }
    }

    private async Task CheckFailureRateAsync(
        MeepleAiDbContext db,
        IQueueStreamService streamService,
        CancellationToken ct)
    {
        var oneHourAgo = DateTimeOffset.UtcNow.AddHours(-1);

        var completedCount = await db.Set<ProcessingJobEntity>()
            .CountAsync(j => j.Status == "Completed" && j.CompletedAt >= oneHourAgo, ct)
            .ConfigureAwait(false);

        var failedCount = await db.Set<ProcessingJobEntity>()
            .CountAsync(j => j.Status == "Failed" && j.CompletedAt >= oneHourAgo, ct)
            .ConfigureAwait(false);

        var total = completedCount + failedCount;
        if (total == 0) return;

        var failureRate = (double)failedCount / total * 100;

        if (failureRate > FailureRateThreshold)
        {
            _logger.LogWarning(
                "High failure rate alert: {Rate:F1}% ({Failed}/{Total}) in the last hour exceeds threshold of {Threshold}%",
                failureRate, failedCount, total, FailureRateThreshold);

            var evt = new QueueStreamEvent(
                QueueStreamEventType.AlertHighFailureRate,
                Guid.Empty,
                new HighFailureRateAlertData(
                    Math.Round(failureRate, 1),
                    FailureRateThreshold,
                    failedCount,
                    total),
                DateTimeOffset.UtcNow);

            await streamService.PublishQueueEventAsync(evt, ct).ConfigureAwait(false);
        }
    }
}
#pragma warning restore CA1031

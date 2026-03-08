using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.DocumentProcessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.DocumentProcessing.Application.Jobs;

/// <summary>
/// Quartz.NET job that processes the next PDF in the queue through the full pipeline.
/// Runs every 10s: picks the highest-priority Queued job, delegates to
/// IPdfProcessingPipelineService, and publishes SSE events via IQueueStreamService.
/// Issue #4730: Processing queue management.
/// </summary>
[DisallowConcurrentExecution]
public sealed class PdfProcessingQuartzJob : IJob
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PdfProcessingQuartzJob> _logger;
    private readonly TimeProvider _timeProvider;

    public PdfProcessingQuartzJob(
        MeepleAiDbContext dbContext,
        IServiceProvider serviceProvider,
        ILogger<PdfProcessingQuartzJob> logger,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        _logger.LogDebug("PdfProcessingQuartzJob started: FireTime={FireTime}", context.FireTimeUtc);

        try
        {
            // Issue #5455: Check queue configuration (IsPaused, concurrency limits)
            var queueConfig = await _dbContext.ProcessingQueueConfigs
                .FirstOrDefaultAsync(c => c.Id == ProcessingQueueConfig.SingletonId, ct)
                .ConfigureAwait(false);

            if (queueConfig is { IsPaused: true })
            {
                _logger.LogDebug("Queue is paused, skipping job pickup");
                return;
            }

            // Check concurrent worker limit
            var maxWorkers = queueConfig?.MaxConcurrentWorkers ?? ProcessingQueueConfig.DefaultMaxConcurrentWorkers;
            var currentProcessing = await _dbContext.ProcessingJobs
                .CountAsync(j => j.Status == nameof(JobStatus.Processing), ct)
                .ConfigureAwait(false);

            if (currentProcessing >= maxWorkers)
            {
                _logger.LogDebug("Max concurrent workers reached ({Current}/{Max}), skipping", currentProcessing, maxWorkers);
                return;
            }

            // Pick the highest-priority queued job (priority DESC = higher value first, then FIFO)
            // AsTracking: Override global NoTracking (PERF-06) so SaveChangesAsync persists mutations
            var jobEntity = await _dbContext.ProcessingJobs
                .AsTracking()
                .Where(j => j.Status == nameof(JobStatus.Queued))
                .OrderByDescending(j => j.Priority)
                .ThenBy(j => j.CreatedAt)
                .FirstOrDefaultAsync(ct).ConfigureAwait(false);

            if (jobEntity is null)
            {
                _logger.LogDebug("No queued PDF processing jobs found");
                return;
            }

            // Load PdfDocument for file path (read-only, but FindAsync respects global tracking config)
            var pdfDoc = await _dbContext.PdfDocuments
                .AsTracking()
                .FirstOrDefaultAsync(p => p.Id == jobEntity.PdfDocumentId, ct)
                .ConfigureAwait(false);

            if (pdfDoc is null)
            {
                _logger.LogError("PDF document {PdfDocumentId} not found for job {JobId}",
                    jobEntity.PdfDocumentId, jobEntity.Id);
                await FailJobAsync(jobEntity, "PDF document not found in database", ct).ConfigureAwait(false);
                return;
            }

            _logger.LogInformation(
                "Processing job {JobId} for PDF {PdfDocumentId} (priority: {Priority})",
                jobEntity.Id, jobEntity.PdfDocumentId, jobEntity.Priority);

            // Mark as processing
            var startedAt = _timeProvider.GetUtcNow();
            jobEntity.Status = nameof(JobStatus.Processing);
            jobEntity.StartedAt = startedAt;
            jobEntity.CurrentStep = nameof(ProcessingStepType.Upload);
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

            // Publish JobStarted SSE event
            await PublishEventSafeAsync(new QueueStreamEvent(
                QueueStreamEventType.JobStarted, jobEntity.Id,
                new JobStartedData(jobEntity.PdfDocumentId, nameof(ProcessingStepType.Upload)),
                startedAt), ct).ConfigureAwait(false);

            // Execute pipeline
            await ExecutePipelineAsync(jobEntity, pdfDoc, startedAt, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "PdfProcessingQuartzJob was cancelled");
        }
#pragma warning disable CA1031 // Top-level catch must not propagate to Quartz scheduler
        catch (Exception ex)
        {
            _logger.LogError(ex, "PdfProcessingQuartzJob encountered an unexpected error");
        }
#pragma warning restore CA1031
    }

    private async Task ExecutePipelineAsync(
        ProcessingJobEntity jobEntity,
        PdfDocumentEntity pdfDoc,
        DateTimeOffset startedAt,
        CancellationToken ct)
    {
        // Load all steps for this job
        // AsTracking: Override global NoTracking so step status mutations are persisted
        var steps = await _dbContext.ProcessingSteps
            .AsTracking()
            .Where(s => s.ProcessingJobId == jobEntity.Id)
            .ToListAsync(ct).ConfigureAwait(false);

        var stepOrder = new[]
        {
            nameof(ProcessingStepType.Upload),
            nameof(ProcessingStepType.Extract),
            nameof(ProcessingStepType.Chunk),
            nameof(ProcessingStepType.Embed),
            nameof(ProcessingStepType.Index)
        };

        try
        {
            // Mark Upload step as running
            await MarkStepRunningAsync(steps, stepOrder[0], startedAt, ct).ConfigureAwait(false);

            // Resolve pipeline service (internal type, resolved via IServiceProvider)
            var pipelineService = _serviceProvider.GetRequiredService<IPdfProcessingPipelineService>();

            // Delegate to pipeline service: validate -> extract -> chunk -> embed -> index -> ready
            await pipelineService.ProcessAsync(
                pdfDoc.Id, pdfDoc.FilePath, jobEntity.UserId, ct).ConfigureAwait(false);

            // Pipeline completed — mark all steps as Completed with SSE events
            var completedAt = _timeProvider.GetUtcNow();
            var totalMs = (completedAt - startedAt).TotalMilliseconds;
            var perStepMs = totalMs / stepOrder.Length;

            foreach (var stepName in stepOrder)
            {
                var step = steps.FirstOrDefault(
                    s => string.Equals(s.StepName, stepName, StringComparison.Ordinal));
                if (step is null) continue;

                step.Status = nameof(StepStatus.Completed);
                step.StartedAt ??= startedAt;
                step.CompletedAt = completedAt;
                step.DurationMs = perStepMs;

                _dbContext.StepLogEntries.Add(new StepLogEntryEntity
                {
                    ProcessingStepId = step.Id,
                    Timestamp = completedAt,
                    Level = "Info",
                    Message = $"{stepName} step completed"
                });

                jobEntity.CurrentStep = stepName;

                await PublishEventSafeAsync(new QueueStreamEvent(
                    QueueStreamEventType.StepCompleted, jobEntity.Id,
                    new StepCompletedData(stepName, perStepMs / 1000.0, null),
                    completedAt), ct).ConfigureAwait(false);
            }

            // Mark job as completed
            jobEntity.Status = nameof(JobStatus.Completed);
            jobEntity.CompletedAt = completedAt;
            jobEntity.CurrentStep = null;
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

            // Publish JobCompleted SSE event
            var totalDurationSec = totalMs / 1000.0;
            await PublishEventSafeAsync(new QueueStreamEvent(
                QueueStreamEventType.JobCompleted, jobEntity.Id,
                new JobCompletedData(totalDurationSec),
                completedAt), ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Job {JobId} completed successfully in {Duration:F1}s",
                jobEntity.Id, totalDurationSec);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            jobEntity.Status = nameof(JobStatus.Cancelled);
            jobEntity.CompletedAt = _timeProvider.GetUtcNow();
            await _dbContext.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed during pipeline execution", jobEntity.Id);

            // Mark the current step as failed
            var failedStepName = jobEntity.CurrentStep ?? stepOrder[0];
            var failedStep = steps.FirstOrDefault(
                s => string.Equals(s.StepName, failedStepName, StringComparison.Ordinal));
            if (failedStep is not null)
            {
                var failTime = _timeProvider.GetUtcNow();
                failedStep.Status = nameof(StepStatus.Failed);
                failedStep.CompletedAt = failTime;
                if (failedStep.StartedAt.HasValue)
                    failedStep.DurationMs = (failTime - failedStep.StartedAt.Value).TotalMilliseconds;

                _dbContext.StepLogEntries.Add(new StepLogEntryEntity
                {
                    ProcessingStepId = failedStep.Id,
                    Timestamp = failTime,
                    Level = "Error",
                    Message = $"Step failed: {ex.Message}"
                });
            }

            var errorMsg = $"Failed at {failedStepName}: {ex.Message}";
            await FailJobAsync(jobEntity, errorMsg, ct).ConfigureAwait(false);
        }
    }

    private async Task MarkStepRunningAsync(
        List<ProcessingStepEntity> steps, string stepName,
        DateTimeOffset startedAt, CancellationToken ct)
    {
        var step = steps.FirstOrDefault(
            s => string.Equals(s.StepName, stepName, StringComparison.Ordinal));
        if (step is null) return;

        step.Status = nameof(StepStatus.Running);
        step.StartedAt = startedAt;

        _dbContext.StepLogEntries.Add(new StepLogEntryEntity
        {
            ProcessingStepId = step.Id,
            Timestamp = startedAt,
            Level = "Info",
            Message = $"Starting {stepName} step"
        });

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private async Task FailJobAsync(ProcessingJobEntity jobEntity, string error, CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow();
        jobEntity.Status = nameof(JobStatus.Failed);
        jobEntity.CompletedAt = now;
        jobEntity.ErrorMessage = error.Length > 500 ? error[..500] : error;

        try
        {
            var saveCt = ct.IsCancellationRequested ? CancellationToken.None : ct;
            await _dbContext.SaveChangesAsync(saveCt).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Best-effort persistence
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist failure state for job {JobId}", jobEntity.Id);
        }
#pragma warning restore CA1031

        await PublishEventSafeAsync(new QueueStreamEvent(
            QueueStreamEventType.JobFailed, jobEntity.Id,
            new JobFailedData(error, jobEntity.CurrentStep, jobEntity.RetryCount),
            now), ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Best-effort SSE event publishing. Failures must not break the pipeline.
    /// </summary>
    private async Task PublishEventSafeAsync(QueueStreamEvent evt, CancellationToken ct)
    {
        try
        {
            var streamService = _serviceProvider.GetRequiredService<IQueueStreamService>();
            await streamService.PublishJobEventAsync(evt, ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // SSE failures must not break pipeline
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish SSE event {EventType} for job {JobId}",
                evt.Type, evt.JobId);
        }
#pragma warning restore CA1031
    }
}

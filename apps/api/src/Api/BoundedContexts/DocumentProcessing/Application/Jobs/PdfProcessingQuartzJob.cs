using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities.DocumentProcessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.DocumentProcessing.Application.Jobs;

/// <summary>
/// Quartz.NET job that processes the next PDF in the queue through the full pipeline.
/// Runs continuously: picks the highest-priority Queued job and processes it.
/// Issue #4730: Processing queue management.
/// </summary>
[DisallowConcurrentExecution]
public sealed class PdfProcessingQuartzJob : IJob
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<PdfProcessingQuartzJob> _logger;
    private readonly TimeProvider _timeProvider;

    public PdfProcessingQuartzJob(
        MeepleAiDbContext dbContext,
        ILogger<PdfProcessingQuartzJob> logger,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogDebug("PdfProcessingQuartzJob started: FireTime={FireTime}", context.FireTimeUtc);

        try
        {
            // Pick the highest-priority queued job
            var jobEntity = await _dbContext.ProcessingJobs
                .Where(j => j.Status == nameof(JobStatus.Queued))
                .OrderBy(j => j.Priority)
                .ThenBy(j => j.CreatedAt)
                .FirstOrDefaultAsync(context.CancellationToken).ConfigureAwait(false);

            if (jobEntity is null)
            {
                _logger.LogDebug("No queued PDF processing jobs found");
                return;
            }

            _logger.LogInformation(
                "Processing PDF job {JobId} for PDF {PdfDocumentId} (priority: {Priority})",
                jobEntity.Id, jobEntity.PdfDocumentId, jobEntity.Priority);

            // Mark as processing
            jobEntity.Status = nameof(JobStatus.Processing);
            jobEntity.StartedAt = _timeProvider.GetUtcNow();
            jobEntity.CurrentStep = nameof(ProcessingStepType.Upload);
            await _dbContext.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);

            // Execute pipeline steps
            await ExecutePipelineAsync(jobEntity, context.CancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "PdfProcessingQuartzJob was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PdfProcessingQuartzJob encountered an unexpected error");
        }
    }

    private async Task ExecutePipelineAsync(ProcessingJobEntity jobEntity, CancellationToken cancellationToken)
    {
        var steps = new[]
        {
            ProcessingStepType.Upload,
            ProcessingStepType.Extract,
            ProcessingStepType.Chunk,
            ProcessingStepType.Embed,
            ProcessingStepType.Index
        };

        try
        {
            foreach (var stepType in steps)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var stepName = stepType.ToString();
                jobEntity.CurrentStep = stepName;

                // Update step status to Running
                var stepEntity = await _dbContext.ProcessingSteps
                    .FirstOrDefaultAsync(s => s.ProcessingJobId == jobEntity.Id && s.StepName == stepName, cancellationToken).ConfigureAwait(false);

                if (stepEntity is not null)
                {
                    var stepStart = _timeProvider.GetUtcNow();
                    stepEntity.Status = nameof(StepStatus.Running);
                    stepEntity.StartedAt = stepStart;
                    await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                    // Add log entry for step start
                    _dbContext.StepLogEntries.Add(new StepLogEntryEntity
                    {
                        ProcessingStepId = stepEntity.Id,
                        Timestamp = stepStart,
                        Level = "Info",
                        Message = $"Starting {stepName} step"
                    });

                    try
                    {
                        // Execute the actual step
                        await ExecuteStepAsync(stepType, jobEntity, cancellationToken).ConfigureAwait(false);

                        // Mark step as completed
                        var stepEnd = _timeProvider.GetUtcNow();
                        stepEntity.Status = nameof(StepStatus.Completed);
                        stepEntity.CompletedAt = stepEnd;
                        stepEntity.DurationMs = (stepEnd - stepStart).TotalMilliseconds;

                        _dbContext.StepLogEntries.Add(new StepLogEntryEntity
                        {
                            ProcessingStepId = stepEntity.Id,
                            Timestamp = stepEnd,
                            Level = "Info",
                            Message = $"{stepName} step completed in {stepEntity.DurationMs:F0}ms"
                        });

                        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        var stepEnd = _timeProvider.GetUtcNow();
                        stepEntity.Status = nameof(StepStatus.Failed);
                        stepEntity.CompletedAt = stepEnd;
                        stepEntity.DurationMs = (stepEnd - stepStart).TotalMilliseconds;

                        _dbContext.StepLogEntries.Add(new StepLogEntryEntity
                        {
                            ProcessingStepId = stepEntity.Id,
                            Timestamp = stepEnd,
                            Level = "Error",
                            Message = $"{stepName} step failed: {ex.Message}"
                        });

                        // Mark job as failed
                        jobEntity.Status = nameof(JobStatus.Failed);
                        jobEntity.CompletedAt = stepEnd;
                        jobEntity.ErrorMessage = $"Failed at {stepName}: {ex.Message}";
                        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                        _logger.LogError(ex, "PDF processing job {JobId} failed at step {Step}", jobEntity.Id, stepName);
                        return;
                    }
                }
            }

            // All steps completed successfully
            var completedAt = _timeProvider.GetUtcNow();
            jobEntity.Status = nameof(JobStatus.Completed);
            jobEntity.CompletedAt = completedAt;
            jobEntity.CurrentStep = null;
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "PDF processing job {JobId} completed successfully in {Duration}ms",
                jobEntity.Id,
                (completedAt - jobEntity.StartedAt!.Value).TotalMilliseconds);
        }
        catch (OperationCanceledException)
        {
            jobEntity.Status = nameof(JobStatus.Cancelled);
            jobEntity.CompletedAt = _timeProvider.GetUtcNow();
            await _dbContext.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Execute a single pipeline step. Delegates to existing processing services.
    /// Will be connected to existing pipeline services in Issue #4731.
    /// </summary>
    private Task ExecuteStepAsync(
        ProcessingStepType stepType,
        ProcessingJobEntity jobEntity,
        CancellationToken cancellationToken)
    {
        // Issue #4731 will wire each step to:
        // Upload → IBlobStorageService
        // Extract → IPdfTextExtractor (via EnhancedPdfProcessingOrchestrator)
        // Chunk → PdfTextProcessingDomainService
        // Embed → Embedding service HTTP client
        // Index → IQdrantService

        _logger.LogInformation(
            "Executing step {Step} for job {JobId} (PDF: {PdfId})",
            stepType, jobEntity.Id, jobEntity.PdfDocumentId);

        return Task.CompletedTask;
    }
}

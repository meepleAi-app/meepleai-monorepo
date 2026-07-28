using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;

/// <summary>
/// Re-queues all failed processing jobs as Low priority.
/// Skips jobs that already have an active (Queued/Processing) job for the same PDF,
/// or that have exceeded max retries.
/// Issue #5456: Bulk reindex failed documents.
/// </summary>
internal sealed class BulkReindexFailedCommandHandler
    : ICommandHandler<BulkReindexFailedCommand, BulkReindexResult>
{
    private readonly IProcessingJobRepository _jobRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MeepleAiDbContext _dbContext;

    public BulkReindexFailedCommandHandler(
        IProcessingJobRepository jobRepository,
        IUnitOfWork unitOfWork,
        MeepleAiDbContext dbContext)
    {
        _jobRepository = jobRepository ?? throw new ArgumentNullException(nameof(jobRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<BulkReindexResult> Handle(
        BulkReindexFailedCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var failedJobs = await _jobRepository.GetAllByStatusAsync(
            JobStatus.Failed, cancellationToken).ConfigureAwait(false);

        // Fix: check available capacity before re-enqueuing to avoid exceeding MaxQueueSize.
        // ProcessingJob.Create() enforces the cap but Retry() does not, so we enforce it here.
        // The real cap is (Queued + Processing < MaxQueueSize), so BOTH in-flight statuses must
        // be subtracted — otherwise with Processing jobs present we over-enqueue past the cap.
        // Mirrors BulkReindexReadyCommandHandler's exact (Queued + Processing) computation.
        var currentQueuedCount = await _jobRepository.CountByStatusAsync(
            JobStatus.Queued, cancellationToken).ConfigureAwait(false);
        var currentProcessingCount = await _jobRepository.CountByStatusAsync(
            JobStatus.Processing, cancellationToken).ConfigureAwait(false);
        var availableSlots = ProcessingJob.MaxQueueSize - (currentQueuedCount + currentProcessingCount);

        var enqueued = 0;
        var skipped = 0;
        var errors = new List<BulkReindexError>();

        foreach (var job in failedJobs)
        {
            // Skip jobs that have exceeded max retries
            if (!job.CanRetry)
            {
                errors.Add(new BulkReindexError(job.Id, "Max retries exceeded"));
                skipped++;
                continue;
            }

            // Skip if there's already an active job for this PDF
            var hasActive = await _jobRepository.ExistsByPdfDocumentIdAsync(
                job.PdfDocumentId, cancellationToken).ConfigureAwait(false);
            if (hasActive)
            {
                errors.Add(new BulkReindexError(job.Id, "Active job already exists for this PDF"));
                skipped++;
                continue;
            }

            // Enforce MaxQueueSize: stop if no slots remain
            if (enqueued >= availableSlots)
            {
                errors.Add(new BulkReindexError(job.Id, "Queue is at maximum capacity"));
                skipped++;
                continue;
            }

            try
            {
                // Retry resets status to Queued and increments RetryCount
                job.Retry();
                // Set priority to Low for bulk reindex
                job.UpdatePriority((int)ProcessingPriority.Low);
                await _jobRepository.UpdateAsync(job, cancellationToken).ConfigureAwait(false);

                // Reset the underlying PDF to Pending so the worker's atomic Pending-only claim
                // (IPdfClaimService.TryClaimPendingAsync) can pick it up. Re-queuing the job alone
                // is a phantom success: the pipeline claims only Pending PDFs, so a Failed PDF is
                // skipped and the job is marked Completed WITHOUT reprocessing. Mirrors the reset in
                // ReindexDocumentCommandHandler (and every other recovery path). IndexerVersion is a
                // provenance label (ADR-086) and is deliberately left untouched. `.AsTracking()` is
                // required because the production MeepleAiDbContext defaults to NoTracking (PERF-06);
                // without it the mutation is silently dropped. Persisted by the SaveChangesAsync
                // below, in the same unit of work as the job update.
                var pdf = await _dbContext.PdfDocuments
                    .AsTracking()
                    .FirstOrDefaultAsync(p => p.Id == job.PdfDocumentId, cancellationToken)
                    .ConfigureAwait(false);
                if (pdf is not null)
                {
                    pdf.ProcessingState = nameof(PdfProcessingState.Pending);
                    pdf.ProcessingError = null;
                    pdf.ProcessedAt = null;
                    pdf.RetryCount = 0;
                    pdf.ErrorCategory = null;
                    pdf.FailedAtState = null;
                }

                enqueued++;
            }
            catch (Exception ex)
            {
                errors.Add(new BulkReindexError(job.Id, ex.Message));
                skipped++;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new BulkReindexResult(enqueued, skipped, errors);
    }
}

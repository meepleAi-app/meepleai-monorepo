using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

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

    public BulkReindexFailedCommandHandler(
        IProcessingJobRepository jobRepository,
        IUnitOfWork unitOfWork)
    {
        _jobRepository = jobRepository ?? throw new ArgumentNullException(nameof(jobRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<BulkReindexResult> Handle(
        BulkReindexFailedCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var failedJobs = await _jobRepository.GetAllByStatusAsync(
            JobStatus.Failed, cancellationToken).ConfigureAwait(false);

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

            try
            {
                // Retry resets status to Queued and increments RetryCount
                job.Retry();
                // Set priority to Low for bulk reindex
                job.UpdatePriority((int)ProcessingPriority.Low);
                await _jobRepository.UpdateAsync(job, cancellationToken).ConfigureAwait(false);
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

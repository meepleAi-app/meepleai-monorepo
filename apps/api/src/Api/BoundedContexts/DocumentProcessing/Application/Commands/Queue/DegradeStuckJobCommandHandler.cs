using System.Globalization;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Observability;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;

/// <summary>
/// Degrades a stuck ProcessingJob and its PdfDocument to Failed.
/// Uses domain methods <c>ProcessingJob.Fail</c> and <c>PdfDocument.MarkAsFailed</c>
/// (TimeProvider-safe timestamps, no direct <c>DateTimeOffset.UtcNow</c> assignment).
/// ErrorCategory.Service (transient) keeps the PDF eligible for RetryFailedPdfsJob.
/// Issue #2689. Design constraint: degrade-only, no re-queue (see #2686 revert).
/// </summary>
internal sealed class DegradeStuckJobCommandHandler
    : ICommandHandler<DegradeStuckJobCommand, DegradeStuckJobResult>
{
    private readonly IProcessingJobRepository _jobRepository;
    private readonly IPdfDocumentRepository _pdfRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPdfIndexingPipeline _indexingPipeline;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DegradeStuckJobCommandHandler> _logger;

    public DegradeStuckJobCommandHandler(
        IProcessingJobRepository jobRepository,
        IPdfDocumentRepository pdfRepository,
        IUnitOfWork unitOfWork,
        IPdfIndexingPipeline indexingPipeline,
        TimeProvider timeProvider,
        ILogger<DegradeStuckJobCommandHandler> logger)
    {
        _jobRepository = jobRepository ?? throw new ArgumentNullException(nameof(jobRepository));
        _pdfRepository = pdfRepository ?? throw new ArgumentNullException(nameof(pdfRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _indexingPipeline = indexingPipeline ?? throw new ArgumentNullException(nameof(indexingPipeline));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DegradeStuckJobResult> Handle(DegradeStuckJobCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var job = await _jobRepository.GetByIdAsync(command.JobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
            return new DegradeStuckJobResult(false, "Job not found");

        // Double-check status: it may have completed between detection and now (race-safe no-op).
        if (job.Status != JobStatus.Processing)
            return new DegradeStuckJobResult(false, $"Job no longer Processing (was {job.Status})");

        var minutes = command.StuckMinutes.ToString("F0", CultureInfo.InvariantCulture);
        var message = $"Processing stalled for {minutes} min past the recovery threshold; degraded to Failed (Issue #2689).";

        try
        {
            // Domain method: TimeProvider-safe, raises JobFailedEvent.
            job.Fail(message, _timeProvider);
            await _jobRepository.UpdateAsync(job, cancellationToken).ConfigureAwait(false);

            // Also degrade the PdfDocument if not already terminal.
            var pdf = await _pdfRepository.GetByIdAsync(job.PdfDocumentId, cancellationToken).ConfigureAwait(false);
            var pdfIsFailed = false;
            if (pdf is not null && pdf.ProcessingState != PdfProcessingState.Ready)
            {
                if (pdf.ProcessingState != PdfProcessingState.Failed)
                {
                    // ErrorCategory.Service (transient) → RetryFailedPdfsJob eligible (RetryCount < 3).
                    pdf.MarkAsFailed(message, ErrorCategory.Service, pdf.ProcessingState);
                    await _pdfRepository.UpdateAsync(pdf, cancellationToken).ConfigureAwait(false);
                }

                pdfIsFailed = true;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // #4609 follow-up (#4660): the PDF is Failed (degraded just now, or already by someone else while the job
            // stayed in Processing), so its vector document (created "processing" by BeginIndexingAsync) must not stay
            // in flight. The retry puts it right again while RetryFailedPdfsJob still has attempts; once they run out
            // nobody would. After the save, so a lost race leaves it alone; idempotent, and a no-op when the PDF failed
            // before the indexing phase; never throws. If the pipeline turns out to be alive and finishes later,
            // IndexAsync completes the row anyway. The text is the category, not an exception message: this column is
            // served to users by pdf-status.
            if (pdfIsFailed)
            {
                await _indexingPipeline.MarkIndexingFailedAsync(
                    job.PdfDocumentId, PdfFailureText.For(ErrorCategory.Service), CancellationToken.None).ConfigureAwait(false);
            }

            _logger.LogWarning(
                "[DegradeStuckJob] Degraded stuck job {JobId} (PDF {PdfId}) to Failed after {Minutes} min",
                command.JobId, job.PdfDocumentId, minutes);

            return new DegradeStuckJobResult(true, "Degraded to Failed");
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Best-effort: another writer already resolved the race — skip silently.
            MeepleAiMetrics.RecordPdfConcurrencyConflict(
                nameof(DegradeStuckJobCommandHandler),
                MeepleAiMetrics.PdfConcurrencyCategories.C);
            _logger.LogWarning(
                ex,
                "[DegradeStuckJob] Concurrency conflict degrading job {JobId}; skipping",
                command.JobId);
            return new DegradeStuckJobResult(false, "Concurrency conflict — skipped");
        }
    }
}


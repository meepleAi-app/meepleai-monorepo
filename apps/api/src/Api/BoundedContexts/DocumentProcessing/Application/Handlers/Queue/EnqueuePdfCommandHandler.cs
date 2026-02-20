using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.DocumentProcessing.Application.Handlers.Queue;

/// <summary>
/// Handles enqueueing a PDF for processing.
/// Issue #4731: Queue commands.
/// </summary>
internal class EnqueuePdfCommandHandler : ICommandHandler<EnqueuePdfCommand, Guid>
{
    private readonly IProcessingJobRepository _jobRepository;
    private readonly IPdfDocumentRepository _pdfRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public EnqueuePdfCommandHandler(
        IProcessingJobRepository jobRepository,
        IPdfDocumentRepository pdfRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _jobRepository = jobRepository ?? throw new ArgumentNullException(nameof(jobRepository));
        _pdfRepository = pdfRepository ?? throw new ArgumentNullException(nameof(pdfRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<Guid> Handle(EnqueuePdfCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Validate PDF exists
        var pdfExists = await _pdfRepository.ExistsAsync(command.PdfDocumentId, cancellationToken)
            .ConfigureAwait(false);
        if (!pdfExists)
            throw new NotFoundException("PdfDocument", command.PdfDocumentId.ToString());

        // Validate not already in queue (active job for same PDF)
        var alreadyQueued = await _jobRepository.ExistsByPdfDocumentIdAsync(command.PdfDocumentId, cancellationToken)
            .ConfigureAwait(false);
        if (alreadyQueued)
            throw new ConflictException($"PDF document '{command.PdfDocumentId}' already has an active job in the queue.");

        // Get current queue size for the MaxQueueSize guard
        var queuedCount = await _jobRepository.CountByStatusAsync(JobStatus.Queued, cancellationToken)
            .ConfigureAwait(false);
        var processingCount = await _jobRepository.CountByStatusAsync(JobStatus.Processing, cancellationToken)
            .ConfigureAwait(false);
        var currentQueueSize = queuedCount + processingCount;

        var job = ProcessingJob.Create(
            command.PdfDocumentId,
            command.UserId,
            command.Priority,
            currentQueueSize,
            _timeProvider);

        await _jobRepository.AddAsync(job, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return job.Id;
    }
}

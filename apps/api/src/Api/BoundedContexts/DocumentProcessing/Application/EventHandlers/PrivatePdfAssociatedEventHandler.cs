using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.BoundedContexts.UserLibrary.Domain.Events;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Application.EventHandlers;

/// <summary>
/// Handles <see cref="PrivatePdfAssociatedEvent"/> by creating a processing job
/// so that private PDFs are queued for text extraction and embedding.
/// Without this handler, private PDFs were uploaded but never processed.
/// </summary>
internal sealed class PrivatePdfAssociatedEventHandler : INotificationHandler<PrivatePdfAssociatedEvent>
{
    private readonly IProcessingJobRepository _jobRepository;
    private readonly IPdfDocumentRepository _pdfRepository;
    private readonly IQueueStreamService _streamService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PrivatePdfAssociatedEventHandler> _logger;

    public PrivatePdfAssociatedEventHandler(
        IProcessingJobRepository jobRepository,
        IPdfDocumentRepository pdfRepository,
        IQueueStreamService streamService,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<PrivatePdfAssociatedEventHandler> logger)
    {
        _jobRepository = jobRepository;
        _pdfRepository = pdfRepository;
        _streamService = streamService;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task Handle(PrivatePdfAssociatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Handling PrivatePdfAssociatedEvent for PdfDocumentId {PdfDocumentId}, UserId {UserId}",
            notification.PdfDocumentId, notification.UserId);

        // Idempotency: skip if a processing job already exists for this PDF
        var jobExists = await _jobRepository
            .ExistsByPdfDocumentIdAsync(notification.PdfDocumentId, cancellationToken)
            .ConfigureAwait(false);

        if (jobExists)
        {
            _logger.LogInformation(
                "Processing job already exists for PdfDocumentId {PdfDocumentId}, skipping",
                notification.PdfDocumentId);
            return;
        }

        // Verify the PDF document exists
        var pdfDocument = await _pdfRepository
            .GetByIdAsync(notification.PdfDocumentId, cancellationToken)
            .ConfigureAwait(false);

        if (pdfDocument is null)
        {
            _logger.LogWarning(
                "PdfDocument {PdfDocumentId} not found when handling PrivatePdfAssociatedEvent, skipping",
                notification.PdfDocumentId);
            return;
        }

        // Create processing job
        var currentQueueSize = await _jobRepository
            .CountByStatusAsync(JobStatus.Queued, cancellationToken)
            .ConfigureAwait(false);

        var job = ProcessingJob.Create(
            notification.PdfDocumentId,
            notification.UserId,
            priority: 0,
            currentQueueSize,
            _timeProvider);

        await _jobRepository.AddAsync(job, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Publish SSE event so the queue dashboard updates in real-time
        var sseEvent = new QueueStreamEvent(
            QueueStreamEventType.JobQueued,
            job.Id,
            new JobQueuedData(notification.PdfDocumentId, notification.UserId, job.Priority),
            _timeProvider.GetUtcNow());

        try
        {
            await _streamService.PublishJobEventAsync(sseEvent, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish SSE event for job {JobId}", job.Id);
        }

        _logger.LogInformation(
            "Created processing job {JobId} for private PDF {PdfDocumentId}",
            job.Id, notification.PdfDocumentId);
    }
}

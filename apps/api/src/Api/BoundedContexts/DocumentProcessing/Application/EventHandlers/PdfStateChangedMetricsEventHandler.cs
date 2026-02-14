using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.DocumentProcessing.Application.EventHandlers;

/// <summary>
/// Event handler for recording processing metrics when PDF state transitions occur.
/// Issue #4212: Historical metrics collection for data-driven ETA calculation.
/// </summary>
internal sealed class PdfStateChangedMetricsEventHandler : INotificationHandler<PdfStateChangedEvent>
{
    private readonly IProcessingMetricsService _metricsService;
    private readonly IPdfDocumentRepository _pdfRepository;
    private readonly ILogger<PdfStateChangedMetricsEventHandler> _logger;

    public PdfStateChangedMetricsEventHandler(
        IProcessingMetricsService metricsService,
        IPdfDocumentRepository pdfRepository,
        ILogger<PdfStateChangedMetricsEventHandler> logger)
    {
        _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
        _pdfRepository = pdfRepository ?? throw new ArgumentNullException(nameof(pdfRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(PdfStateChangedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        // Only record metrics when transitioning from a processing step to the next step
        // (not for Pending → Uploading or Failed transitions)
        if (!ShouldRecordMetric(notification.PreviousState, notification.NewState))
        {
            _logger.LogDebug(
                "Skipping metric recording for transition {Previous} → {New}",
                notification.PreviousState,
                notification.NewState);
            return;
        }

        try
        {
            // Get PDF document to retrieve metadata
            var pdf = await _pdfRepository.GetByIdAsync(notification.PdfDocumentId, cancellationToken)
                .ConfigureAwait(false);

            if (pdf == null)
            {
                _logger.LogWarning(
                    "PDF {PdfId} not found for metrics recording",
                    notification.PdfDocumentId);
                return;
            }

            // Calculate duration for the completed step
            var duration = CalculateStepDuration(pdf, notification.PreviousState);

            if (duration == null)
            {
                _logger.LogDebug(
                    "Could not calculate duration for step {Step} of PDF {PdfId}",
                    notification.PreviousState,
                    notification.PdfDocumentId);
                return;
            }

            // Record metric
            await _metricsService.RecordStepDurationAsync(
                notification.PdfDocumentId,
                notification.PreviousState,
                duration.Value,
                pdf.FileSize.Bytes,
                pdf.PageCount ?? 1,
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Recorded metric: PdfId={PdfId}, Step={Step}, Duration={Duration}s",
                notification.PdfDocumentId,
                notification.PreviousState,
                duration.Value.TotalSeconds);
        }
        catch (Exception ex)
        {
            // Don't fail the main operation if metrics recording fails
            _logger.LogError(
                ex,
                "Failed to record metric for PDF {PdfId} step {Step}",
                notification.PdfDocumentId,
                notification.PreviousState);
        }
    }

    /// <summary>
    /// Determines if a metric should be recorded for this state transition.
    /// Only record metrics when completing a processing step.
    /// </summary>
    private static bool ShouldRecordMetric(PdfProcessingState previousState, PdfProcessingState newState)
    {
        // Record metrics only for forward progression through processing steps
        // Skip: Pending → Uploading (no work yet), any → Failed (not completion)
        return previousState switch
        {
            PdfProcessingState.Uploading when newState == PdfProcessingState.Extracting => true,
            PdfProcessingState.Extracting when newState == PdfProcessingState.Chunking => true,
            PdfProcessingState.Chunking when newState == PdfProcessingState.Embedding => true,
            PdfProcessingState.Embedding when newState == PdfProcessingState.Indexing => true,
            PdfProcessingState.Indexing when newState == PdfProcessingState.Ready => true,
            _ => false
        };
    }

    /// <summary>
    /// Calculates the duration spent in a specific processing step.
    /// Uses timestamp fields from PdfDocument entity.
    /// </summary>
    private static TimeSpan? CalculateStepDuration(
        Api.BoundedContexts.DocumentProcessing.Domain.Entities.PdfDocument pdf,
        PdfProcessingState completedStep)
    {
        var startTime = completedStep switch
        {
            PdfProcessingState.Uploading => pdf.UploadingStartedAt,
            PdfProcessingState.Extracting => pdf.ExtractingStartedAt,
            PdfProcessingState.Chunking => pdf.ChunkingStartedAt,
            PdfProcessingState.Embedding => pdf.EmbeddingStartedAt,
            PdfProcessingState.Indexing => pdf.IndexingStartedAt,
            _ => null
        };

        if (!startTime.HasValue)
            return null;

        var endTime = completedStep switch
        {
            PdfProcessingState.Uploading => pdf.ExtractingStartedAt,
            PdfProcessingState.Extracting => pdf.ChunkingStartedAt,
            PdfProcessingState.Chunking => pdf.EmbeddingStartedAt,
            PdfProcessingState.Embedding => pdf.IndexingStartedAt,
            PdfProcessingState.Indexing => pdf.ProcessedAt,
            _ => null
        };

        if (!endTime.HasValue)
            return null;

        return endTime.Value - startTime.Value;
    }
}

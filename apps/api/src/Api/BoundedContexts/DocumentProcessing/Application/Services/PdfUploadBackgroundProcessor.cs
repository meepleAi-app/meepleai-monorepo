using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Background processor for PDF uploads. Delegates the extract-chunk-embed-index pipeline
/// to <see cref="IPdfProcessingPipelineService"/> and adds upload-specific lifecycle:
/// quota confirmation on success, quota release on failure, and PdfStateChangedEvent publishing.
/// </summary>
/// <remarks>
/// Registered as <strong>scoped</strong>. The handler resolves it inside an
/// <see cref="Microsoft.Extensions.DependencyInjection.IServiceScopeFactory"/> scope so it can
/// safely run in a fire-and-forget background task without sharing the request's DbContext.
/// </remarks>
internal sealed class PdfUploadBackgroundProcessor : IPdfUploadBackgroundProcessor
{
    private readonly IPdfProcessingPipelineService _pipeline;
    private readonly IPdfUploadQuotaService _quotaService;
    private readonly IMediator _mediator;
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<PdfUploadBackgroundProcessor> _logger;

    public PdfUploadBackgroundProcessor(
        IPdfProcessingPipelineService pipeline,
        IPdfUploadQuotaService quotaService,
        IMediator mediator,
        MeepleAiDbContext db,
        ILogger<PdfUploadBackgroundProcessor> logger)
    {
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _quotaService = quotaService ?? throw new ArgumentNullException(nameof(quotaService));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ProcessAsync(
        string pdfDocumentId,
        string filePath,
        Guid uploadedByUserId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("[UploadProcessor] Starting background processing for PDF {PdfId}, User {UserId}",
            pdfDocumentId, uploadedByUserId);

        if (!Guid.TryParse(pdfDocumentId, out var pdfGuid))
        {
            _logger.LogError("[UploadProcessor] Invalid PDF ID format {PdfId}", pdfDocumentId);
            await _quotaService.ReleaseQuotaAsync(uploadedByUserId, pdfDocumentId, CancellationToken.None).ConfigureAwait(false);
            return;
        }

        try
        {
            // Delegate the full extract → chunk → embed → index pipeline
            await _pipeline.ProcessAsync(pdfGuid, filePath, uploadedByUserId, cancellationToken).ConfigureAwait(false);

            // Verify the pipeline succeeded before publishing events and confirming quota
            var pdfDoc = await _db.PdfDocuments
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == pdfGuid, CancellationToken.None).ConfigureAwait(false);

            if (pdfDoc == null)
            {
                _logger.LogWarning("[UploadProcessor] PDF {PdfId} not found after processing", pdfDocumentId);
                await _quotaService.ReleaseQuotaAsync(uploadedByUserId, pdfDocumentId, CancellationToken.None).ConfigureAwait(false);
                return;
            }

            var readyState = nameof(PdfProcessingState.Ready);
            if (!string.Equals(pdfDoc.ProcessingState, readyState, StringComparison.Ordinal))
            {
                // Pipeline marked it Failed — release quota
                _logger.LogWarning("[UploadProcessor] PDF {PdfId} ended in state {State} — releasing quota",
                    pdfDocumentId, pdfDoc.ProcessingState);
                await _quotaService.ReleaseQuotaAsync(uploadedByUserId, pdfDocumentId, CancellationToken.None).ConfigureAwait(false);
                return;
            }

            // Publish PdfStateChangedEvent so downstream handlers fire:
            // AutoCreateAgentOnPdfReadyHandler (admin PDFs), PdfNotificationEventHandler, PdfStateChangedMetricsEventHandler.
            // Must be published after pipeline SaveChanges so handlers can query the updated entity.
            await _mediator.Publish(
                new PdfStateChangedEvent(pdfGuid, PdfProcessingState.Indexing, PdfProcessingState.Ready, uploadedByUserId),
                CancellationToken.None).ConfigureAwait(false);

            // Two-Phase Quota (#1743): Confirm quota (Phase 2)
            await _quotaService.ConfirmQuotaAsync(uploadedByUserId, pdfDocumentId, CancellationToken.None).ConfigureAwait(false);

            _logger.LogInformation("[UploadProcessor] PDF {PdfId} processed successfully", pdfDocumentId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[UploadProcessor] Processing cancelled for PDF {PdfId}", pdfDocumentId);
            await _quotaService.ReleaseQuotaAsync(uploadedByUserId, pdfDocumentId, CancellationToken.None).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Background service must catch all to release quota
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UploadProcessor] Processing failed for PDF {PdfId}", pdfDocumentId);
            await _quotaService.ReleaseQuotaAsync(uploadedByUserId, pdfDocumentId, CancellationToken.None).ConfigureAwait(false);
        }
#pragma warning restore CA1031
    }
}

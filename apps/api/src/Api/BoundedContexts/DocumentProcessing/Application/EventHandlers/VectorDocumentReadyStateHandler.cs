using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Observability;
using Api.SharedKernel.Application.IntegrationEvents;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Application.EventHandlers;

/// <summary>
/// Handles VectorDocumentReadyIntegrationEvent to update PDF document processing state.
/// Compensating update: sets ProcessingState=Ready when vector indexing completes
/// but the PDF state machine wasn't properly transitioned.
/// Issue #5237: Decoupled from KnowledgeBase — state update now within DocumentProcessing context.
/// </summary>
internal sealed class VectorDocumentReadyStateHandler
    : INotificationHandler<VectorDocumentReadyIntegrationEvent>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<VectorDocumentReadyStateHandler> _logger;

    public VectorDocumentReadyStateHandler(
        MeepleAiDbContext dbContext,
        ILogger<VectorDocumentReadyStateHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Handle(
        VectorDocumentReadyIntegrationEvent evt,
        CancellationToken cancellationToken)
    {
        // Only update if state is not already Ready or Failed
        if (string.Equals(evt.CurrentProcessingState, nameof(PdfProcessingState.Ready), StringComparison.Ordinal)
            || string.Equals(evt.CurrentProcessingState, nameof(PdfProcessingState.Failed), StringComparison.Ordinal))
        {
            return;
        }

        // .AsTracking() overrides the global QueryTrackingBehavior.NoTracking default (PERF-06,
        // InfrastructureServiceExtensions.cs). Without it the entity is detached and the
        // ProcessingState=Ready + IndexerVersion writes below are a silent no-op at SaveChangesAsync,
        // making this compensating handler dead. Mirrors ExtractPdfTextCommandHandler /
        // IndexPdfCommandHandler (#3288-class fix).
        var pdfEntity = await _dbContext.PdfDocuments
            .AsTracking()
            .FirstOrDefaultAsync(p => p.Id == evt.PdfDocumentId, cancellationToken)
            .ConfigureAwait(false);

        if (pdfEntity != null)
        {
            pdfEntity.ProcessingState = nameof(PdfProcessingState.Ready);
            // Issue #3269 (SP3): stamp the current indexer version on the compensating Ready
            // transition too, so vector-indexed docs aren't left with a null (legacy) marker.
            // Null-coalescing preserves an explicit version already stamped by a reindex reset.
            pdfEntity.IndexerVersion ??= IndexerVersionRegistry.Current.Version;
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                MeepleAiMetrics.RecordPdfConcurrencyConflict(
                    nameof(VectorDocumentReadyStateHandler),
                    MeepleAiMetrics.PdfConcurrencyCategories.B);
                _logger.LogWarning(ex,
                    "Concurrency conflict on PdfDocument {PdfId} in {Handler} (Category B) — admin mutation wins, pipeline will re-read on next tick",
                    evt.PdfDocumentId, nameof(VectorDocumentReadyStateHandler));
                return; // CRITICAL: do not throw — caller (MediatR publish) must see success
            }

            _logger.LogInformation(
                "Compensating update: set ProcessingState=Ready for PdfDocument {PdfId} (was {PrevState}) after vector indexing",
                evt.PdfDocumentId,
                evt.CurrentProcessingState);
        }
    }
}

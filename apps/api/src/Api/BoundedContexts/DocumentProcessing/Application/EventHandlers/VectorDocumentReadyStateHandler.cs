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

        // #4339 / #4609: the event arrives synchronously from IPdfIndexingPipeline.IndexAsync, inside the
        // call of the pipeline that is indexing the PDF. A PDF in Indexing is that pipeline's: it moves it
        // to Ready itself right after IndexAsync returns. Moving it here made the pipeline's finalization
        // fail ("Cannot transition from Ready state"), which is what #4339 fixed when IndexAsync was
        // called BEFORE the passages and the embeddings were written (so "the index has no embeddings
        // yet" told a half-way PDF from a stuck one). Since #4609 IndexAsync is called LAST, after they
        // are saved, so the embeddings are always there and only the state can say the PDF is not stuck.
        // What this handler still compensates is a PDF in any other non-final state whose index is
        // written.
        if (string.Equals(evt.CurrentProcessingState, nameof(PdfProcessingState.Indexing), StringComparison.Ordinal))
        {
            return;
        }

        var indexWritten = await _dbContext.PgVectorEmbeddings
            .AsNoTracking()
            .AnyAsync(e => e.VectorDocumentId == evt.DocumentId, cancellationToken)
            .ConfigureAwait(false);
        if (!indexWritten)
        {
            _logger.LogDebug(
                "No compensation for PdfDocument {PdfId}: its index {DocumentId} has no embeddings yet, the pipeline is still writing it",
                evt.PdfDocumentId,
                evt.DocumentId);
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

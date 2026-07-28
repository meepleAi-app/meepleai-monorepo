using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.Observability;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Handler for ReindexDocumentCommand. Issue #1673 estende il flusso con:
/// 1. Risoluzione versione: <c>command.IndexerVersion ?? pdf.IndexerVersion ?? Current</c>.
/// 2. Conflict guard: se il documento è in pipeline (stati non-terminali), 409 Conflict.
/// 3. Persistenza della versione risolta su <c>pdf.IndexerVersion</c>.
/// 4. Audit via <c>[AuditableAction("DocumentReindex", "Document", Level=2)]</c> sul command.
/// </summary>
internal sealed class ReindexDocumentCommandHandler : ICommandHandler<ReindexDocumentCommand>
{
    // Stati pre-terminali. Reindex bloccato finché non si raggiunge Ready o Failed.
    // Derived from Enum.GetNames so adding a new pre-terminal state to PdfProcessingState
    // (or renaming an existing one) automatically updates this set — no manual sync needed. Issue #1801.
    private static readonly HashSet<string> InFlightStates =
        new(
            Enum.GetNames<PdfProcessingState>()
                .Except(
                    new[]
                    {
                        nameof(PdfProcessingState.Ready),
                        nameof(PdfProcessingState.Failed),
                    },
                    StringComparer.Ordinal),
            StringComparer.Ordinal);

    private readonly MeepleAiDbContext _dbContext;
    private readonly IMediator _mediator;
    private readonly ILogger<ReindexDocumentCommandHandler> _logger;

    public ReindexDocumentCommandHandler(
        MeepleAiDbContext dbContext,
        IMediator mediator,
        ILogger<ReindexDocumentCommandHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(ReindexDocumentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var pdf = await _dbContext.PdfDocuments
            .AsTracking()
            .FirstOrDefaultAsync(p => p.Id == command.PdfId, cancellationToken)
            .ConfigureAwait(false);

        if (pdf is null)
        {
            throw new NotFoundException("PdfDocument", command.PdfId.ToString());
        }

        // Conflict guard: rifiuta il reindex se la pipeline è in-flight.
        if (InFlightStates.Contains(pdf.ProcessingState))
        {
            throw new ConflictException(
                $"Document {command.PdfId} is currently being processed (state={pdf.ProcessingState}); cannot reindex until it reaches Ready or Failed.");
        }

        // Risoluzione versione: explicit → stored → current.
        var resolvedVersion = command.IndexerVersion
            ?? pdf.IndexerVersion
            ?? IndexerVersionRegistry.Current.Version;

        // Cancella chunks associati.
        var chunks = await _dbContext.TextChunks
            .Where(tc => tc.PdfDocumentId == command.PdfId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (chunks.Count > 0)
        {
            _dbContext.TextChunks.RemoveRange(chunks);
        }

        // Stage the destructive reset (chunk delete + field mutations) but DO NOT commit it on its
        // own. The reset MUST commit atomically with the new ProcessingJob: the old code saved the
        // reset first and then swallowed any enqueue failure (queue full / transient) in a broad
        // catch — a phantom success that left the PDF reset-to-Pending with its chunks deleted but
        // no job to reprocess it, stranding the document (bug-hunt B10, #3269; B4 amplifies it by
        // stamping IndexerVersion=target so no recovery selector re-picks the strand). EnqueuePdfCommand
        // shares this scoped DbContext, so wrapping both in one explicit transaction makes the reset
        // + job commit together — or not at all.
        pdf.ProcessingState = nameof(PdfProcessingState.Pending);
        pdf.ProcessedAt = null;
        pdf.ProcessingError = null;
        pdf.RetryCount = 0;
        pdf.ErrorCategory = null;
        pdf.FailedAtState = null;
        pdf.IndexerVersion = resolvedVersion;

        // The production Npgsql context uses a retrying execution strategy, which forbids
        // user-initiated transactions unless they run inside the strategy's executed delegate so a
        // transient-failure retry re-runs the WHOLE transaction as one retriable unit.
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _dbContext.Database
                .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Persist the reset within the transaction (not yet committed).
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                // Enqueue within the SAME transaction — EnqueuePdfCommandHandler's SaveChanges runs
                // on the shared DbContext under this ambient transaction, so the job is staged
                // alongside the reset and both commit together below.
                await _mediator.Send(
                    new EnqueuePdfCommand(command.PdfId, pdf.UploadedByUserId, Priority: 0),
                    cancellationToken).ConfigureAwait(false);

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation(
                    "Reindexed PDF {PdfId} with version {IndexerVersion} enqueued for processing",
                    command.PdfId, resolvedVersion);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                MeepleAiMetrics.RecordPdfConcurrencyConflict(
                    nameof(ReindexDocumentCommandHandler),
                    MeepleAiMetrics.PdfConcurrencyCategories.A);
                _logger.LogWarning(ex,
                    "Concurrency conflict on PdfDocument {PdfId} in {Handler} (Category A)",
                    command.PdfId, nameof(ReindexDocumentCommandHandler));
                throw new ConflictException(
                    $"Document {command.PdfId} was modified by another concurrent operation; please retry.");
            }
            catch (ConflictException ex)
            {
                // Queue full, or an active job already exists for this PDF. The transaction rolls
                // back, so the destructive reset is undone — the document stays in its pre-reindex
                // state with its chunks intact (no strand). Surface a retryable 409 instead of the
                // old phantom success. In the BulkReindexReadyCommandHandler fan-out this is caught
                // per-document and the batch continues with the remaining candidates.
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogWarning(ex,
                    "Could not enqueue reindex for PDF {PdfId} (queue full or already queued); reset rolled back",
                    command.PdfId);
                throw;
            }
        }).ConfigureAwait(false);
    }
}

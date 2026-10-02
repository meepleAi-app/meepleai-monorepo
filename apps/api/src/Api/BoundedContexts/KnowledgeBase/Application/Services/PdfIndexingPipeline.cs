using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Observability;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// EF + MediatR implementation of <see cref="IPdfIndexingPipeline"/>.
///
/// Centralises the "PDF → VectorDocument indexed" write path so domain
/// events fire structurally instead of via the tactical compensating
/// publish that #2243 had to add inline at four call sites.
///
/// Concurrency: a <see cref="DbUpdateConcurrencyException"/> on the EF
/// save is logged as a metric (category B: admin mutation wins, pipeline
/// will re-read on the next tick) — same policy the original 5 call sites
/// applied, kept intact to preserve existing semantics.
/// </summary>
internal sealed class PdfIndexingPipeline : IPdfIndexingPipeline
{
    private readonly MeepleAiDbContext _db;
    private readonly IMediator _mediator;
    private readonly TimeProvider _timeProvider;
    private readonly ISemanticResponseCache _semanticCache;
    private readonly ILogger<PdfIndexingPipeline> _logger;

    public PdfIndexingPipeline(
        MeepleAiDbContext db,
        IMediator mediator,
        TimeProvider timeProvider,
        ISemanticResponseCache semanticCache,
        ILogger<PdfIndexingPipeline> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _semanticCache = semanticCache ?? throw new ArgumentNullException(nameof(semanticCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Invalidates the semantic response cache for a game whose index just changed.
    /// <para>
    /// ISSUE #3982 — why this lives in the pipeline and not in the callers. The invalidation used to
    /// sit in <c>IndexPdfCommandHandler</c>, one of FOUR callers of <see cref="IndexAsync"/>:
    /// the other three — <c>UploadPdfCommandHandler.Processing</c>,
    /// <c>CompleteChunkedUploadCommandHandler</c> and <c>PdfProcessingPipelineService</c> — wrote a
    /// new index and left day-old answers being served for the full 24h TTL. The pipeline already
    /// knew it had four callers (see the comment on <c>wasNotCompletedBefore</c> below); the cache
    /// contract says "called on re-index", and this is where the re-index happens. Putting it here
    /// makes the omission not expressible.
    /// </para>
    /// <para>
    /// Best-effort by design, like the AI-cache invalidation in the delete handlers: a cache that
    /// fails to clear must not fail an index that is already committed. The cost of swallowing is a
    /// stale answer for at most the TTL; the cost of throwing is a document indexed in the database
    /// and reported as failed.
    /// </para>
    /// <para>
    /// No invalidation when <paramref name="gameId"/> is null: nothing is ever cached without a
    /// game. <c>AskQuestionQueryHandler</c> keys both reads and writes on <c>query.GameId</c>
    /// (:238, :496), which is a real game — so there are no entries under a placeholder to clear.
    /// </para>
    /// </summary>
    private async Task InvalidateSemanticCacheSafelyAsync(Guid? gameId, Guid pdfDocumentId, CancellationToken cancellationToken)
    {
        if (!gameId.HasValue)
        {
            return;
        }

        try
        {
            await _semanticCache.InvalidateGameAsync(gameId.Value, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Do not catch general exception types — see <para> above
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex,
                "Failed to invalidate the semantic response cache for game {GameId} after indexing PDF {PdfDocumentId} — stale answers may be served until the TTL expires",
                gameId.Value, pdfDocumentId);
        }
    }

    public async Task IndexAsync(
        Guid pdfDocumentId,
        Guid? gameId,
        Guid? sharedGameId,
        int chunkCount,
        int totalCharacters,
        string language,
        CancellationToken cancellationToken = default)
    {
        if (chunkCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(chunkCount), "chunkCount must be positive");
        if (totalCharacters < 0)
            throw new ArgumentOutOfRangeException(nameof(totalCharacters), "totalCharacters cannot be negative");
        if (string.IsNullOrWhiteSpace(language))
            throw new ArgumentException("language cannot be empty", nameof(language));

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        // AsTracking required: we may update an existing row in-place.
        var existing = await _db.VectorDocuments
            .AsTracking()
            .FirstOrDefaultAsync(v => v.PdfDocumentId == pdfDocumentId, cancellationToken)
            .ConfigureAwait(false);

        // Track whether this call drives the document INTO the "completed" state.
        // IndexPdfCommandHandler's flow creates the row in "processing" first
        // (so it has an Id to use as PgVectorEmbedding FK), then transitions
        // it to "completed" at the end of indexing — we want the domain event
        // on THAT transition, not on the original insert. The other 3 paths
        // create the row directly in "completed", which also counts.
        var wasNotCompletedBefore = existing is null
            || !string.Equals(existing.IndexingStatus, "completed", StringComparison.Ordinal);

        VectorDocument? newDomainAggregate = null;

        if (existing is null)
        {
            // Build the domain aggregate so the constructor raises
            // VectorDocumentIndexedEvent — this is the contract Sub #1's
            // tactical publish was emulating.
            // #2284 issue 2: thread totalCharacters to the domain so it survives the
            // mapper round-trip (mapper now writes domain.TotalCharacters instead of 0).
            newDomainAggregate = VectorDocument.Create(
                pdfDocumentId: pdfDocumentId,
                gameId: gameId ?? Guid.Empty,
                totalChunks: chunkCount,
                language: language,
                sharedGameId: sharedGameId,
                totalCharacters: totalCharacters);

            existing = new VectorDocumentEntity
            {
                Id = newDomainAggregate.Id,
                GameId = gameId,
                SharedGameId = sharedGameId,
                PdfDocumentId = pdfDocumentId,
                ChunkCount = chunkCount,
                TotalCharacters = totalCharacters,
                IndexingStatus = "completed",
                IndexedAt = nowUtc
            };
            _db.VectorDocuments.Add(existing);
        }
        else
        {
            existing.IndexingStatus = "completed";
            existing.ChunkCount = chunkCount;
            existing.TotalCharacters = totalCharacters;
            existing.IndexedAt = nowUtc;
            // Clear stale error from a previous failed run, if any —
            // matches the explicit reset in the legacy IndexPdfCommandHandler path.
            existing.IndexingError = null;
            // Heal a missing SharedGameId link on the pre-existing "processing" row.
            // Root cause of the has_knowledge_base drift: when the row was created in
            // "processing" state without a SharedGameId, the VectorDocumentIndexedEvent
            // emitted on the completed-transition below carried a null SharedGameId, so
            // VectorDocumentIndexedForKbFlagHandler skipped the flag update even though the
            // caller knows the SharedGameId here. Backfill it so the event — and the row —
            // both carry the link.
            if (existing.SharedGameId is null && sharedGameId is not null)
            {
                existing.SharedGameId = sharedGameId;
            }
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            MeepleAiMetrics.RecordPdfConcurrencyConflict(
                nameof(PdfIndexingPipeline),
                MeepleAiMetrics.PdfConcurrencyCategories.B);
            _logger.LogWarning(ex,
                "Concurrency conflict on VectorDocument for PDF {PdfDocumentId} (Category B) — admin mutation wins, pipeline will re-read on next tick",
                pdfDocumentId);
            return;
        }

        // #3982 — qui, e non dopo il bail: il bail che segue riguarda la RIPUBBLICAZIONE DI EVENTI,
        // non la cache. Un re-index su un documento gia' `completed` riscrive i chunk e rende
        // stantie le risposte in cache esattamente come il primo index, quindi va invalidata anche
        // su quel percorso. Dopo il SaveChanges perche' invalidare una cache per un indice che non
        // si e' committato aprirebbe una finestra in cui si riempie di nuovo col vecchio contenuto.
        await InvalidateSemanticCacheSafelyAsync(gameId, pdfDocumentId, cancellationToken).ConfigureAwait(false);

        if (!wasNotCompletedBefore)
        {
            // Already "completed" before this call — re-publishing the event
            // would be noisy and risks duplicate side effects in handlers
            // that aren't strictly idempotent. has_knowledge_base is already
            // true downstream; bail.
            return;
        }

        // Publish AFTER commit so projection handlers see the committed row.
        // CancellationToken.None: once the row is persisted, we MUST attempt
        // to publish — silently dropping events here would resurrect the
        // exact failure mode #2242 was opened to close.
        if (newDomainAggregate is not null)
        {
            foreach (var domainEvent in newDomainAggregate.DomainEvents)
            {
                await _mediator.Publish(domainEvent, CancellationToken.None).ConfigureAwait(false);
            }
            newDomainAggregate.ClearDomainEvents();
        }
        else
        {
            // Existing aggregate transitioned processing → completed.
            // Emit explicitly so the projection handler sees the change.
            await _mediator.Publish(
                new VectorDocumentIndexedEvent(
                    documentId: existing.Id,
                    gameId: existing.GameId ?? Guid.Empty,
                    chunkCount: existing.ChunkCount,
                    // Prefer the healed row value; fall back to the caller-supplied id so the
                    // KB-flag projection never receives a null SharedGameId when the caller knew it.
                    sharedGameId: existing.SharedGameId ?? sharedGameId),
                CancellationToken.None).ConfigureAwait(false);
        }
    }
}

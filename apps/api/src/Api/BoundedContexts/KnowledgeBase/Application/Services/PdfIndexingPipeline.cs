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
    private readonly ILogger<PdfIndexingPipeline> _logger;

    public PdfIndexingPipeline(
        MeepleAiDbContext db,
        IMediator mediator,
        TimeProvider timeProvider,
        ILogger<PdfIndexingPipeline> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.Observability;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Handles <see cref="DeleteKbDocumentCommand"/>.
///
/// Deletion order:
///   1. Guard: load PdfDocument — 404 if absent.
///   2. Agent cascade: detach the document from every consuming AgentDefinition.KbCardIds.
///   3. pgvector embeddings: delete raw embeddings by VectorDocumentId.
///   4. EF delete: remove PdfDocument; cascade removes TextChunks + VectorDocument.
///   5. Blob: delete physical file (best-effort).
///   6. Cache: invalidate AI response cache (best-effort).
///
/// Issue #1653: F3-FU-4 — Admin delete KB document action.
/// </summary>
internal sealed class DeleteKbDocumentCommandHandler : ICommandHandler<DeleteKbDocumentCommand>
{
    private readonly MeepleAiDbContext _db;
    private readonly IAgentDefinitionRepository _agents;
    private readonly IVectorStoreAdapter _vectorStore;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IAiResponseCacheService _cacheService;
    private readonly Api.BoundedContexts.KnowledgeBase.Application.Services.ISemanticResponseCache _semanticCache;
    private readonly IMediator? _mediator;
    private readonly ILogger<DeleteKbDocumentCommandHandler> _logger;

    public DeleteKbDocumentCommandHandler(
        MeepleAiDbContext db,
        IAgentDefinitionRepository agents,
        IVectorStoreAdapter vectorStore,
        IBlobStorageService blobStorageService,
        IAiResponseCacheService cacheService,
        Api.BoundedContexts.KnowledgeBase.Application.Services.ISemanticResponseCache semanticCache,
        ILogger<DeleteKbDocumentCommandHandler> logger,
        IMediator? mediator = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _agents = agents ?? throw new ArgumentNullException(nameof(agents));
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _blobStorageService = blobStorageService ?? throw new ArgumentNullException(nameof(blobStorageService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _semanticCache = semanticCache ?? throw new ArgumentNullException(nameof(semanticCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediator = mediator;
    }

    public async Task Handle(DeleteKbDocumentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var id = command.Id;

        // 1. Guard
        // Issue #3866: `.AsTracking()` is REQUIRED — the DbContext default is NoTracking (PERF-06),
        // so `_db.PdfDocuments.Remove(doc)` further down had to ATTACH the instance this read
        // returned. Reached in a loop over several documents in one scope (OrphanPdfCleanupSeeder)
        // that either collided on identity or left the row in place: the orphan cleanup reported
        // success and the PDF was still there.
        var doc = await _db.PdfDocuments
            .AsTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken).ConfigureAwait(false);

        if (doc == null)
            throw new NotFoundException("KbDocument", id.ToString());

        // 2. Agent cascade — detach this document from every consuming agent's KbCardIds
        var consumingAgents = await _agents
            .GetByConsumedDocumentAsync(id, cancellationToken).ConfigureAwait(false);

        foreach (var agent in consumingAgents)
        {
            agent.UpdateKbCardIds(agent.KbCardIds.Where(kbId => kbId != id));
            await _agents.UpdateAsync(agent, cancellationToken).ConfigureAwait(false);
        }
        // No intermediate SaveChanges: the agent detach stays tracked and is flushed together
        // with the document removal in the single SaveChangesAsync below, so detach + delete
        // commit atomically in one transaction (also wrapped by [AtomicAudit]).

        // 3. pgvector embeddings — delete raw embeddings (pgvector_embeddings table)
        //    before removing the VectorDocument (to which they link).
        var vectorDocId = await _db.VectorDocuments
            .Where(v => v.PdfDocumentId == id)
            .Select(v => (Guid?)v.Id)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (vectorDocId.HasValue)
        {
            await _vectorStore.DeleteByVectorDocumentIdAsync(vectorDocId.Value, cancellationToken)
                .ConfigureAwait(false);
            _logger.LogInformation(
                "Deleted pgvector embeddings for VectorDocumentId={VectorDocId}", vectorDocId.Value);
        }

        // 4. EF delete — cascade removes TextChunks + VectorDocument
        var storageGameId = (doc.PrivateGameId ?? doc.SharedGameId)?.ToString() ?? string.Empty;
        var coverR2Key = doc.CoverR2Key;

        _db.PdfDocuments.Remove(doc);
        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            MeepleAiMetrics.RecordPdfConcurrencyConflict(
                nameof(DeleteKbDocumentCommandHandler),
                MeepleAiMetrics.PdfConcurrencyCategories.A);
            _logger.LogWarning(ex,
                "Concurrency conflict on PdfDocument {PdfId} in {Handler} (Category A)",
                id, nameof(DeleteKbDocumentCommandHandler));
            throw new ConflictException(
                $"Document {id} was modified by another concurrent operation; please retry.");
        }
        _logger.LogInformation(
            "Deleted KB document {DocId} (detached from {AgentCount} consuming agents)",
            id, consumingAgents.Count);

        // Issue #1831 AC: raise PdfDeletedDomainEvent so PdfDeletedEventHandler
        // evicts the L4 cover thumb/preview from R2. Published AFTER the
        // SaveChangesAsync succeeded (not via IDomainEventCollector pre-save):
        // a stale event left in the collector after the ConflictException path
        // would otherwise be drained by a later SaveChangesAsync in the same
        // scope and trigger R2 cleanup for a PDF that was never deleted.
        if (_mediator is not null && !string.IsNullOrWhiteSpace(coverR2Key))
        {
            await _mediator
                .Publish(new PdfDeletedDomainEvent(id, coverR2Key), cancellationToken)
                .ConfigureAwait(false);
        }

        // 5. Blob — best-effort physical file deletion
        await DeletePhysicalFileAsync(id.ToString(), storageGameId, cancellationToken).ConfigureAwait(false);

        // 6. Cache — best-effort AI response cache invalidation
        await InvalidateCacheSafelyAsync(storageGameId, "KB doc deletion", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes the physical PDF file from blob storage (best-effort; failures are logged, not re-thrown).
    /// Copied from <see cref="DeletePdfCommandHandler"/> per ADR pattern.
    /// </summary>
    private async Task DeletePhysicalFileAsync(string pdfId, string gameId, CancellationToken cancellationToken)
    {
        try
        {
            await _blobStorageService.DeleteAsync(pdfId, BlobCategory.Pdf, gameId, cancellationToken)
                .ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125    // Sections of code should not be commented out
        // ADAPTER PATTERN: Physical file deletion is best-effort cleanup;
        // failures must not block document metadata deletion (file may already be gone).
#pragma warning restore S125
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex, "Error deleting physical file for KB document {PdfId}", pdfId);
        }
    }

    /// <summary>
    /// Invalidates the AI response cache for the game (best-effort; failures are logged, not re-thrown).
    /// Copied from <see cref="DeletePdfCommandHandler"/> per ADR pattern.
    /// </summary>
    private async Task InvalidateCacheSafelyAsync(string gameId, string operation, CancellationToken cancellationToken)
    {
        try
        {
            await _cacheService.InvalidateGameAsync(gameId, cancellationToken).ConfigureAwait(false);
        // #3982 — anche la cache SEMANTICA, non solo quella delle risposte AI.
        //
        // Finora la cancellazione invalidava `IAiResponseCacheService` e lasciava intatta
        // `ISemanticResponseCache`: le risposte costruite sul manuale cancellato continuavano a
        // essere servite per le 24h del suo TTL, citazioni comprese. Il caso che lo rende netto e'
        // la rimozione per copyright o su richiesta — la cancellazione viene dichiarata compiuta
        // mentre il contenuto e' ancora rispondibile.
        //
        // L'iniezione attraversa il confine verso KnowledgeBase, e segue un precedente dello stesso
        // bounded context: `IndexPdfCommandHandler` (DocumentProcessing) inietta
        // `ISemanticResponseCache` dal 2026-09. Non e' un pattern nuovo introdotto qui.
        //
        // `Guid.TryParse` e non `Parse`: l'interfaccia AI lavora su `string`, quella semantica su
        // `Guid`, e un id non parsabile non deve fare esplodere una cancellazione gia' avvenuta.
        if (Guid.TryParse(gameId, out var semanticGameId))
        {
            try
            {
                await _semanticCache.InvalidateGameAsync(semanticGameId, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // Do not catch general exception types — best-effort, come sopra
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogWarning(ex,
                    "Invalid operation invalidating the semantic cache for game {GameId} after {Operation} — stale answers may be served until the TTL expires",
                    gameId, operation);
            }
        }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex,
                "Invalid operation invalidating AI cache for game {GameId} after {Operation}", gameId, operation);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125    // Sections of code should not be commented out
        // CLEANUP PATTERN: Cache invalidation is best-effort optimization;
        // failures must not interrupt document deletion workflow.
#pragma warning restore S125
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex,
                "Unexpected error invalidating AI cache for game {GameId} after {Operation}", gameId, operation);
        }
    }
}

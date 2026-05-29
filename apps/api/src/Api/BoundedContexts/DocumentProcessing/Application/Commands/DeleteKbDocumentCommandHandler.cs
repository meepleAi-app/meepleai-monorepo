using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
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
    private readonly ILogger<DeleteKbDocumentCommandHandler> _logger;

    public DeleteKbDocumentCommandHandler(
        MeepleAiDbContext db,
        IAgentDefinitionRepository agents,
        IVectorStoreAdapter vectorStore,
        IBlobStorageService blobStorageService,
        IAiResponseCacheService cacheService,
        ILogger<DeleteKbDocumentCommandHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _agents = agents ?? throw new ArgumentNullException(nameof(agents));
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _blobStorageService = blobStorageService ?? throw new ArgumentNullException(nameof(blobStorageService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(DeleteKbDocumentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var id = command.Id;

        // 1. Guard
        var doc = await _db.PdfDocuments
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

        if (consumingAgents.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "Detached KB document {DocId} from {AgentCount} consuming agents",
                id, consumingAgents.Count);
        }

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
        _db.PdfDocuments.Remove(doc);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Deleted KB document {DocId}", id);

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

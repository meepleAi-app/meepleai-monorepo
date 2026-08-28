using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.EntityRelationships.Domain.Aggregates;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.EntityRelationships.Domain.Repositories;
using Api.Infrastructure;
using Api.Services;
using Api.Services.Exceptions;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

internal class DeletePdfCommandHandler : ICommandHandler<DeletePdfCommand, PdfDeleteResult>
{
    private readonly MeepleAiDbContext _db;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IAiResponseCacheService _cacheService;
    private readonly IEntityLinkRepository _entityLinkRepository;
    private readonly IMediator? _mediator;
    private readonly ILogger<DeletePdfCommandHandler> _logger;

    public DeletePdfCommandHandler(
        MeepleAiDbContext db,
        IBlobStorageService blobStorageService,
        IAiResponseCacheService cacheService,
        ILogger<DeletePdfCommandHandler> logger,
        IEntityLinkRepository entityLinkRepository,
        IMediator? mediator = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _blobStorageService = blobStorageService ?? throw new ArgumentNullException(nameof(blobStorageService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _entityLinkRepository = entityLinkRepository ?? throw new ArgumentNullException(nameof(entityLinkRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediator = mediator;
    }

    public async Task<PdfDeleteResult> Handle(DeletePdfCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var pdfId = command.PdfId;

        try
        {
            var pdfGuid = Guid.Parse(pdfId);
            // Issue #3866: `.AsTracking()` is REQUIRED on both reads of this handler. The DbContext
            // default is NoTracking (PERF-06), so each `Remove()` below had to ATTACH what the read
            // returned. When the same row was reachable more than once in the scope that threw
            // "cannot be tracked because another instance with the same key value is already being
            // tracked" — wrapped as a PdfStorageException, i.e. a 500 on a delete that should
            // succeed. A delete path needs one instance per row, which is what tracking gives.
            var pdfDoc = await _db.PdfDocuments
                .AsTracking()
                .FirstOrDefaultAsync(p => p.Id == pdfGuid, cancellationToken).ConfigureAwait(false);

            if (pdfDoc == null)
            {
                return new PdfDeleteResult(false, "PDF not found", null);
            }

            var gameId = pdfDoc.SharedGameId;
            var coverR2Key = pdfDoc.CoverR2Key;

            var storageGameIdForUnlink = (pdfDoc.PrivateGameId ?? pdfDoc.SharedGameId)?.ToString() ?? string.Empty;

            // Issue #2949 (DEC-6): ref-counting delete. A PDF may be linked from
            // multiple games (Task 2 of #2943 introduced dedup reuse via EntityLink).
            // Deleting the record/blob/vectors while another game still links it would
            // orphan those games' KB cards. Gate the destructive path on the last link.
            var linkCount = await _entityLinkRepository
                .GetCountForEntityAsync(MeepleEntityType.KbCard, pdfGuid, cancellationToken)
                .ConfigureAwait(false);

            if (linkCount > 1)
            {
                // Issue #2949 finding #1: catalog dedup is GLOBAL, so pdfDoc.SharedGameId
                // stays pinned to the original uploader game even after other games link
                // the same PDF via reuse. When the command carries an explicit caller game
                // (e.g. RemoveRagFromSharedGameCommandHandler cleaning up that specific
                // game), remove THAT game's link. Only fall back to SharedGameId for call
                // sites where caller == uploader's game (no distinct game context exists).
                var linkRemoved = await RemoveCallerLinkAsync(
                    pdfGuid,
                    command.CallerGameId ?? pdfDoc.SharedGameId,
                    cancellationToken).ConfigureAwait(false);

                var remainingLinks = linkRemoved ? linkCount - 1 : linkCount;

                _logger.LogInformation(
                    "PDF {PdfId} still referenced by {RemainingLinks} game link(s) after unlink — record/blob/vectors preserved",
                    pdfId, remainingLinks);

                return new PdfDeleteResult(true, "PDF unlinked from game (still referenced by other games)", storageGameIdForUnlink);
            }

            // Delete associated vector document and vectors from pgvector
            await DeleteVectorDocumentAsync(pdfGuid, pdfId, cancellationToken).ConfigureAwait(false);

            // Delete PDF document record
            _db.PdfDocuments.Remove(pdfDoc);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Deleted PDF document record {PdfId}", pdfId);

            // Issue #1831 AC: raise PdfDeletedDomainEvent so PdfDeletedEventHandler
            // evicts the L4 cover thumb/preview from R2. Published AFTER
            // SaveChangesAsync succeeds (not via IDomainEventCollector pre-save):
            // a stale event left in the collector after a DbUpdateException
            // catch path would otherwise be drained by a later SaveChangesAsync
            // in the same scope and trigger R2 cleanup for a PDF that was never
            // actually deleted from the database.
            if (_mediator is not null && !string.IsNullOrWhiteSpace(coverR2Key))
            {
                await _mediator
                    .Publish(new PdfDeletedDomainEvent(pdfGuid, coverR2Key), cancellationToken)
                    .ConfigureAwait(false);
            }

            // Delegate physical file deletion to BlobStorageService
            var storageGameId = (pdfDoc.PrivateGameId ?? gameId)?.ToString() ?? string.Empty;
            await DeletePhysicalFileAsync(pdfId, storageGameId, cancellationToken).ConfigureAwait(false);

            // Invalidate cache
            await InvalidateCacheSafelyAsync(storageGameId, "PDF deletion", cancellationToken).ConfigureAwait(false);

            return new PdfDeleteResult(true, "PDF deleted successfully", storageGameId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict deleting PDF {PdfId} - treating as already deleted", pdfId);
            return new PdfDeleteResult(false, "PDF not found", null);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error deleting PDF {PdfId}", pdfId);
            throw new PdfStorageException("Failed to delete PDF metadata: Database error occurred.", ex);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // CQRS HANDLER BOUNDARY: Wrap all unexpected exceptions during PDF deletion
        // into domain-specific PdfStorageException for consistent error handling upstream
#pragma warning restore S125
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Unexpected error deleting PDF {PdfId}", pdfId);
            throw new PdfStorageException($"Failed to delete PDF: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deletes vector document from database (pgvector).
    /// </summary>
    private async Task DeleteVectorDocumentAsync(Guid pdfGuid, string pdfId, CancellationToken cancellationToken)
    {
        var vectorDoc = await _db.VectorDocuments
            .AsTracking()
            .FirstOrDefaultAsync(v => v.PdfDocumentId == pdfGuid, cancellationToken).ConfigureAwait(false);

        if (vectorDoc == null)
        {
            return;
        }

        _db.VectorDocuments.Remove(vectorDoc);
        _logger.LogInformation("Removed vector document for PDF {PdfId}", pdfId);
    }

    /// <summary>
    /// Removes only the calling game's Game→KbCard link (the last-link case is
    /// handled by the destructive path in <see cref="Handle"/>). Used when other
    /// games still reference this PDF, so the record must survive.
    /// </summary>
    /// <returns><c>true</c> if a matching link was found and removed; otherwise <c>false</c>.</returns>
    private async Task<bool> RemoveCallerLinkAsync(Guid pdfGuid, Guid? callerGameId, CancellationToken cancellationToken)
    {
        if (callerGameId is null || callerGameId.Value == Guid.Empty)
        {
            // No owning-game link to remove for this call; the record survives
            // because other links exist (linkCount > 1 already established).
            return false;
        }

        var links = await _entityLinkRepository
            .GetForEntityAsync(
                MeepleEntityType.KbCard,
                pdfGuid,
                linkType: EntityLinkType.RelatedTo,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var callerLink = links.FirstOrDefault(l =>
            l.SourceEntityType == MeepleEntityType.Game &&
            l.SourceEntityId == callerGameId.Value &&
            l.TargetEntityType == MeepleEntityType.KbCard &&
            l.TargetEntityId == pdfGuid);

        if (callerLink is null)
        {
            return false;
        }

        // Issue #3866: `GetForEntityAsync` is a read query and — correctly — does not track, so
        // `Remove()` had to ATTACH the instance it returned. When the same row was already tracked
        // elsewhere in the scope that threw "cannot be tracked because another instance with the
        // same key value is already being tracked", wrapped as a PdfStorageException: a 500 on a
        // delete that should succeed. `GetByIdAsync` is the tracked read the repository documents
        // for exactly this purpose (#3858), so the delete goes through it.
        var trackedLink = await _entityLinkRepository
            .GetByIdAsync(callerLink.Id, cancellationToken)
            .ConfigureAwait(false);

        if (trackedLink is null)
        {
            return false;
        }

        // Follow-up: this hard-deletes the link (Remove) instead of the aggregate's
        // soft-delete (link.Delete()) used by DeleteEntityLinkCommandHandler.
        _entityLinkRepository.Remove(trackedLink);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Deletes physical PDF file from blob storage.
    /// </summary>
    private async Task DeletePhysicalFileAsync(string pdfId, string gameId, CancellationToken cancellationToken)
    {
        try
        {
            await _blobStorageService.DeleteAsync(pdfId, BlobCategory.Pdf, gameId, cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // ADAPTER PATTERN: Physical file deletion is best-effort cleanup;
        // failures must not block PDF metadata deletion (file may already be gone).
#pragma warning restore S125
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex, "Error deleting physical file for PDF {PdfId}", pdfId);
        }
    }

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
            _logger.LogWarning(ex, "Invalid operation invalidating AI cache for game {GameId} after {Operation}", gameId, operation);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // CLEANUP PATTERN: Cache invalidation is best-effort optimization;
        // failures must not interrupt PDF deletion workflow.
#pragma warning restore S125
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex, "Unexpected error invalidating AI cache for game {GameId} after {Operation}", gameId, operation);
        }
    }
}


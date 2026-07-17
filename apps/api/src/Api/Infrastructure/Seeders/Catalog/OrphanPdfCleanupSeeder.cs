using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders.Catalog;

/// <summary>
/// Issue #2907: retroactively removes orphan <c>pdf_documents</c> — rows whose
/// <c>shared_game_id</c> points to a SharedGame that no longer exists OR has been soft-deleted
/// (the SharedGames <c>!IsDeleted</c> global query filter catches both). Follow-up to #2904,
/// which stopped NEW orphans forming but left the existing backlog behind.
///
/// Delegates each deletion to the canonical <c>DeleteKbDocumentCommand</c> via <see cref="IMediator"/>
/// so the full cascade (agent detach → pgvector embeddings → EF cascade of text_chunks +
/// vector_document → best-effort blob + cache + PdfDeletedDomainEvent) is reused rather than
/// re-implemented. Idempotent (a removed row vanishes from the anti-join / the command 404s on
/// re-run) and resilient (per-orphan failures are logged and skipped).
/// </summary>
internal static class OrphanPdfCleanupSeeder
{
    /// <summary>
    /// Returns the ids of PDFs whose <c>SharedGameId</c> is set but does not resolve to a live
    /// (non-soft-deleted) SharedGame. PDFs with a null SharedGameId are out of scope.
    /// </summary>
    internal static Task<List<Guid>> FindOrphanPdfIdsAsync(MeepleAiDbContext db, CancellationToken ct)
    {
        var validGameIds = db.SharedGames.Select(g => g.Id);
        return db.PdfDocuments
            .Where(p => p.SharedGameId != null && !validGameIds.Contains(p.SharedGameId.Value))
            .Select(p => p.Id)
            .ToListAsync(ct);
    }

    public static async Task CleanupAsync(
        MeepleAiDbContext db,
        IMediator mediator,
        ILogger logger,
        CancellationToken ct)
    {
        var orphanIds = await FindOrphanPdfIdsAsync(db, ct).ConfigureAwait(false);
        if (orphanIds.Count == 0)
        {
            logger.LogInformation("OrphanPdfCleanupSeeder: no orphan PDFs found");
            return;
        }

        var removed = 0;
        var skipped = 0;
        foreach (var pdfId in orphanIds)
        {
            try
            {
                await mediator.Send(new DeleteKbDocumentCommand(pdfId), ct).ConfigureAwait(false);
                removed++;
            }
            catch (NotFoundException)
            {
                // Already gone (concurrent delete or a previous run) — idempotent no-op.
                skipped++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "OrphanPdfCleanupSeeder: failed to remove orphan PDF {PdfId}. Continuing.", pdfId);
                db.ChangeTracker.Clear();
                skipped++;
            }
        }

        logger.LogInformation(
            "OrphanPdfCleanupSeeder completed: {Removed} orphan PDFs removed, {Skipped} skipped",
            removed, skipped);
    }
}

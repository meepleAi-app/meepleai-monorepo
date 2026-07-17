using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders.Catalog;

/// <summary>
/// Retroactively removes dead <c>pdf_documents</c> rows in two classes:
///
/// <list type="number">
///   <item><description><b>Orphans (#2907)</b> — <c>shared_game_id</c> points to a SharedGame
///   that no longer exists OR has been soft-deleted (the SharedGames <c>!IsDeleted</c> global
///   query filter catches both). Follow-up to #2904, which stopped NEW orphans forming but left
///   the existing backlog behind.</description></item>
///   <item><description><b>Legacy empty shells (#3075)</b> — a pre-migration <c>FilePath</c>
///   (<c>seed/…</c>, never written by the modern seeder/upload which use <c>pdfs/{id}/…</c>) with
///   <b>zero</b> associated <c>text_chunks</c>. These contribute nothing to RAG but flip to
///   <c>Failed</c> on the missing blob when disturbed (<c>StalePdfRecoveryService</c> / a reindex),
///   pinning <c>seed_state</c> to <c>partial_failed</c>. The parent SharedGame is typically valid
///   (a modern <c>&lt;game&gt;_rulebook.pdf</c> sibling holds the real chunks) so #2907 misses them.</description></item>
/// </list>
///
/// Delegates each deletion to the canonical <c>DeleteKbDocumentCommand</c> via <see cref="IMediator"/>
/// so the full cascade (agent detach → pgvector embeddings → EF cascade of text_chunks +
/// vector_document → best-effort blob + cache + PdfDeletedDomainEvent) is reused rather than
/// re-implemented. Idempotent (a removed row vanishes from the query / the command 404s on
/// re-run) and resilient (per-row failures are logged and skipped).
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

    /// <summary>
    /// Issue #3075: returns the ids of legacy-scheme empty-shell PDFs — a pre-migration
    /// <c>FilePath</c> (<c>seed/…</c>) with zero associated <c>text_chunks</c>. Scheme-anchored:
    /// the <c>seed/</c> runtime path is frozen (the modern seeder/upload always write
    /// <c>pdfs/{id}/…</c>), so such a row is never in-flight — no risk of deleting a record
    /// mid-embedding. The zero-chunk guard protects any row still serving RAG (has chunks),
    /// even if legacy-scheme.
    /// </summary>
    internal static Task<List<Guid>> FindLegacyShellPdfIdsAsync(MeepleAiDbContext db, CancellationToken ct)
    {
        return db.PdfDocuments
            .Where(p => p.FilePath.StartsWith("seed/")
                        && !db.TextChunks.Any(tc => tc.PdfDocumentId == p.Id))
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
        var legacyShellIds = await FindLegacyShellPdfIdsAsync(db, ct).ConfigureAwait(false);

        // Dedupe: a row can be both an orphan (missing parent) AND a legacy shell (seed/ path,
        // 0 chunks). Union collapses the overlap so it is only deleted once.
        var idsToDelete = orphanIds.Union(legacyShellIds).ToList();
        if (idsToDelete.Count == 0)
        {
            logger.LogInformation("OrphanPdfCleanupSeeder: no orphan or legacy-shell PDFs found");
            return;
        }

        var removed = 0;
        var skipped = 0;
        foreach (var pdfId in idsToDelete)
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
                logger.LogWarning(ex, "OrphanPdfCleanupSeeder: failed to remove PDF {PdfId}. Continuing.", pdfId);
                db.ChangeTracker.Clear();
                skipped++;
            }
        }

        logger.LogInformation(
            "OrphanPdfCleanupSeeder completed: {Removed} removed, {Skipped} skipped "
            + "({Orphans} orphans, {Shells} legacy shells found)",
            removed, skipped, orphanIds.Count, legacyShellIds.Count);
    }
}

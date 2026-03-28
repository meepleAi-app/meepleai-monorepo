using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Commands.IncrementalRagBackup;

/// <summary>
/// Handles <see cref="IncrementalRagBackupCommand"/> by exporting a single document's
/// RAG bundle (chunks + embeddings) to the "latest" snapshot and updating its manifest entry.
/// All errors are caught and returned as a failure result — backup failures must not
/// disrupt indexing or other processes.
/// </summary>
internal sealed class IncrementalRagBackupCommandHandler
    : IRequestHandler<IncrementalRagBackupCommand, IncrementalRagBackupResult>
{
    private const string LatestSnapshotId = "latest";

    private readonly MeepleAiDbContext _db;
    private readonly IRagExportService _exportService;
    private readonly ILogger<IncrementalRagBackupCommandHandler> _logger;

    public IncrementalRagBackupCommandHandler(
        MeepleAiDbContext db,
        IRagExportService exportService,
        ILogger<IncrementalRagBackupCommandHandler> logger)
    {
        _db = db;
        _exportService = exportService;
        _logger = logger;
    }

#pragma warning disable CA1031 // Non-blocking backup: must not propagate exceptions to caller
    public async Task<IncrementalRagBackupResult> Handle(
        IncrementalRagBackupCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // ── 1. Find VectorDocument by PdfDocumentId ───────────────────────
            var vectorDoc = await _db.VectorDocuments
                .Include(v => v.PdfDocument)
                .Include(v => v.Game)
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.PdfDocumentId == request.PdfDocumentId, cancellationToken)
                .ConfigureAwait(false);

            if (vectorDoc is null)
            {
                _logger.LogWarning(
                    "IncrementalRagBackup: VectorDocument not found for PdfDocumentId={PdfDocumentId}",
                    request.PdfDocumentId);
                return new IncrementalRagBackupResult(false, $"VectorDocument not found for PdfDocumentId {request.PdfDocumentId}");
            }

            // ── 2. Load chunks and embeddings ─────────────────────────────────
            var chunks = await _db.TextChunks
                .Where(c => c.PdfDocumentId == request.PdfDocumentId)
                .OrderBy(c => c.ChunkIndex)
                .AsNoTracking()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var embeddings = await _db.PgVectorEmbeddings
                .Where(e => e.VectorDocumentId == vectorDoc.Id)
                .OrderBy(e => e.ChunkIndex)
                .AsNoTracking()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            // ── 3. Export document bundle to "latest" snapshot ────────────────
            var gameName = vectorDoc.Game?.Name ?? "unknown-game";
            var pdfDoc = vectorDoc.PdfDocument;

            await _exportService.ExportDocumentBundleAsync(
                LatestSnapshotId,
                vectorDoc,
                pdfDoc,
                gameName,
                chunks,
                embeddings,
                cancellationToken)
                .ConfigureAwait(false);

            // ── 4. Read existing manifest, update entry, write back ───────────
            var manifest = await _exportService.ReadManifestAsync(LatestSnapshotId, cancellationToken)
                .ConfigureAwait(false);

            var gameSlug = RagBackupPathHelper.Slugify(gameName);
            var documentPath = RagBackupPathHelper.BuildDocumentPath(LatestSnapshotId, gameSlug, pdfDoc.Id);

            var newEntry = new RagExportManifestEntry(
                PdfDocumentId: pdfDoc.Id,
                GameSlug: gameSlug,
                GameName: gameName,
                Path: documentPath,
                Chunks: chunks.Count,
                Language: pdfDoc.Language);

            List<RagExportManifestEntry> updatedDocuments;
            if (manifest is null)
            {
                updatedDocuments = [newEntry];
            }
            else
            {
                // Replace existing entry for this document, or add new
                updatedDocuments = manifest.Documents
                    .Where(e => e.PdfDocumentId != pdfDoc.Id)
                    .ToList();
                updatedDocuments.Add(newEntry);
            }

            // Recalculate total embeddings: keep existing manifest total, adjust for the replaced entry.
            // For a new entry: add embeddings.Count; for a replaced entry: subtract old count (unknown),
            // so we track only what we know. The manifest primarily serves as a document registry;
            // exact embedding totals are tracked per bundle in metadata.json.
            var previousEmbeddingCount = manifest?.TotalEmbeddings ?? 0;
            var isReplacing = manifest?.Documents.Any(e => e.PdfDocumentId == pdfDoc.Id) ?? false;
            var totalEmbeddings = isReplacing ? previousEmbeddingCount : previousEmbeddingCount + embeddings.Count;

            var updatedManifest = new RagExportManifest(
                ExportVersion: "1.0",
                ExportedAt: DateTimeOffset.UtcNow,
                TotalDocuments: updatedDocuments.Count,
                TotalChunks: updatedDocuments.Sum(e => e.Chunks),
                TotalEmbeddings: totalEmbeddings,
                EmbeddingModel: vectorDoc.EmbeddingModel,
                Documents: updatedDocuments);

            await _exportService.WriteManifestAsync(LatestSnapshotId, updatedManifest, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "IncrementalRagBackup complete: pdfDocumentId={PdfDocumentId} game={GameName} " +
                "chunks={ChunkCount} embeddings={EmbeddingCount}",
                request.PdfDocumentId, gameName, chunks.Count, embeddings.Count);

            return new IncrementalRagBackupResult(true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "IncrementalRagBackup failed for PdfDocumentId={PdfDocumentId}",
                request.PdfDocumentId);
            return new IncrementalRagBackupResult(false, ex.Message);
        }
    }
#pragma warning restore CA1031
}

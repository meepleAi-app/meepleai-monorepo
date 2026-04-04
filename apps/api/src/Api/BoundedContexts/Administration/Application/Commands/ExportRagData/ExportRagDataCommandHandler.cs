using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Commands.ExportRagData;

/// <summary>
/// Handles full RAG data export: queries all completed vector documents, exports each bundle
/// (chunks + embeddings) via <see cref="IRagExportService"/>, and writes a manifest.
/// Supports dry-run mode for counting without writing files.
/// </summary>
internal sealed class ExportRagDataCommandHandler : IRequestHandler<ExportRagDataCommand, ExportRagDataResult>
{
    private readonly MeepleAiDbContext _db;
    private readonly IRagExportService _exportService;
    private readonly ILogger<ExportRagDataCommandHandler> _logger;

    public ExportRagDataCommandHandler(
        MeepleAiDbContext db,
        IRagExportService exportService,
        ILogger<ExportRagDataCommandHandler> logger)
    {
        _db = db;
        _exportService = exportService;
        _logger = logger;
    }

    public async Task<ExportRagDataResult> Handle(ExportRagDataCommand request, CancellationToken cancellationToken)
    {
        var snapshotId = DateTime.UtcNow.ToString("yyyy-MM-dd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);

        // ── 1. Build document query ───────────────────────────────────────────
        var query = _db.VectorDocuments
            .Where(v => v.IndexingStatus == "completed")
            .Include(v => v.PdfDocument)
            .Include(v => v.Game)
            .AsNoTracking();

        if (request.GameIdFilter is not null && Guid.TryParse(request.GameIdFilter, out var gameIdGuid))
        {
            query = query.Where(v => v.GameId == gameIdGuid);
        }

        var vectorDocs = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "ExportRagData {Mode}: found {Count} completed vector documents (snapshotId={SnapshotId})",
            request.DryRun ? "DRY RUN" : "EXECUTE", vectorDocs.Count, snapshotId);

        // ── 2. Dry-run: count only ────────────────────────────────────────────
        if (request.DryRun)
        {
            var dryChunks = 0;
            var dryEmbeddings = 0;

            foreach (var doc in vectorDocs)
            {
                dryChunks += await _db.TextChunks
                    .Where(c => c.PdfDocumentId == doc.PdfDocumentId)
                    .CountAsync(cancellationToken)
                    .ConfigureAwait(false);

                dryEmbeddings += await _db.PgVectorEmbeddings
                    .Where(e => e.VectorDocumentId == doc.Id)
                    .CountAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            return new ExportRagDataResult(
                TotalDocuments: vectorDocs.Count,
                TotalChunks: dryChunks,
                TotalEmbeddings: dryEmbeddings,
                Skipped: 0,
                Failed: 0,
                IsDryRun: true,
                SnapshotId: snapshotId,
                Errors: new List<string>());
        }

        // ── 3. Full export ────────────────────────────────────────────────────
        var errors = new List<string>();
        var manifestEntries = new List<RagExportManifestEntry>();
        var totalChunks = 0;
        var totalEmbeddings = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var vectorDoc in vectorDocs)
        {
            cancellationToken.ThrowIfCancellationRequested();

#pragma warning disable CA1031 // Service boundary: export must continue on individual document errors
            try
            {
                var gameName = vectorDoc.Game?.Name ?? "unknown-game";
                var gameSlug = RagBackupPathHelper.Slugify(gameName);
                var pdfDoc = vectorDoc.PdfDocument;

                // Query chunks and embeddings for this document
                var chunks = await _db.TextChunks
                    .Where(c => c.PdfDocumentId == vectorDoc.PdfDocumentId)
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

                // Skip documents with no data
                if (chunks.Count == 0 && embeddings.Count == 0)
                {
                    _logger.LogWarning(
                        "Skipping document {PdfDocumentId}: no chunks or embeddings found",
                        vectorDoc.PdfDocumentId);
                    skipped++;
                    continue;
                }

                // Export the document bundle
                await _exportService.ExportDocumentBundleAsync(
                    snapshotId,
                    vectorDoc,
                    pdfDoc,
                    gameName,
                    chunks,
                    embeddings,
                    cancellationToken)
                    .ConfigureAwait(false);

                totalChunks += chunks.Count;
                totalEmbeddings += embeddings.Count;

                var documentPath = RagBackupPathHelper.BuildDocumentPath(snapshotId, gameSlug, pdfDoc.Id);
                manifestEntries.Add(new RagExportManifestEntry(
                    PdfDocumentId: pdfDoc.Id,
                    GameSlug: gameSlug,
                    GameName: gameName,
                    Path: documentPath,
                    Chunks: chunks.Count,
                    Embeddings: embeddings.Count,
                    Language: pdfDoc.Language));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors.Add($"Error exporting document {vectorDoc.PdfDocumentId}: {ex.Message}");
                failed++;
                _logger.LogWarning(ex, "Failed to export document {PdfDocumentId}", vectorDoc.PdfDocumentId);
            }
#pragma warning restore CA1031
        }

        // ── 4. Write manifest ─────────────────────────────────────────────────
        var manifest = new RagExportManifest(
            ExportVersion: "1.0",
            ExportedAt: DateTimeOffset.UtcNow,
            TotalDocuments: manifestEntries.Count,
            TotalChunks: totalChunks,
            TotalEmbeddings: manifestEntries.Sum(d => d.Embeddings),
            EmbeddingModel: vectorDocs.Count > 0 ? vectorDocs[0].EmbeddingModel : "nomic-embed-text",
            Documents: manifestEntries);

        await _exportService.WriteManifestAsync(snapshotId, manifest, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "ExportRagData complete: snapshotId={SnapshotId} documents={Documents} chunks={Chunks} " +
            "embeddings={Embeddings} skipped={Skipped} failed={Failed}",
            snapshotId, manifestEntries.Count, totalChunks, totalEmbeddings, skipped, failed);

        return new ExportRagDataResult(
            TotalDocuments: manifestEntries.Count,
            TotalChunks: totalChunks,
            TotalEmbeddings: totalEmbeddings,
            Skipped: skipped,
            Failed: failed,
            IsDryRun: false,
            SnapshotId: snapshotId,
            Errors: errors);
    }
}

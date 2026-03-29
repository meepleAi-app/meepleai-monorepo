using System.Text.Json;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Services;

/// <summary>
/// Writes document bundles (metadata.json + chunks + embeddings in JSONL + Parquet format)
/// using the backup storage service and serializers.
/// </summary>
internal sealed class RagExportService : IRagExportService
{
    private const string ExportVersion = "1.0";

    private readonly IRagBackupStorageService _storage;
    private readonly ILogger<RagExportService> _logger;

    public RagExportService(
        IRagBackupStorageService storage,
        ILogger<RagExportService> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ExportDocumentBundleAsync(
        string snapshotId,
        VectorDocumentEntity vectorDoc,
        PdfDocumentEntity pdfDoc,
        string gameName,
        List<TextChunkEntity> chunks,
        List<PgVectorEmbeddingEntity> embeddings,
        CancellationToken ct = default)
    {
        var gameSlug = RagBackupPathHelper.Slugify(gameName);
        var basePath = RagBackupPathHelper.BuildDocumentPath(snapshotId, gameSlug, pdfDoc.Id);

        // ── 1. Build and write metadata.json ──────────────────────────────────
        var metadata = BuildDocumentMetadata(vectorDoc, pdfDoc, gameName, gameSlug, chunks, embeddings);
        var metadataBytes = RagExportSerializer.ToJsonBytes(metadata);
        await _storage.WriteFileAsync($"{basePath}/metadata.json", metadataBytes, ct).ConfigureAwait(false);

        // ── 2. Map chunks and write chunks.jsonl + chunks.parquet ─────────────
        var chunkLines = chunks
            .Select(c => new RagExportChunkLine(
                ChunkIndex: c.ChunkIndex,
                PageNumber: c.PageNumber,
                Content: c.Content,
                CharacterCount: c.CharacterCount))
            .ToList();

        var chunksJsonlBytes = RagExportSerializer.ToJsonlBytes(chunkLines);
        await _storage.WriteFileAsync($"{basePath}/chunks.jsonl", chunksJsonlBytes, ct).ConfigureAwait(false);

        var chunksParquetBytes = await RagParquetSerializer.SerializeChunksAsync(chunkLines).ConfigureAwait(false);
        await _storage.WriteFileAsync($"{basePath}/chunks.parquet", chunksParquetBytes, ct).ConfigureAwait(false);

        // ── 3. Map embeddings and write embeddings.jsonl + embeddings.parquet ──
        var embeddingLines = embeddings
            .Select(e => new RagExportEmbeddingLine(
                ChunkIndex: e.ChunkIndex,
                PageNumber: e.PageNumber,
                TextContent: e.TextContent,
                Vector: e.Vector.ToArray(),
                Model: e.Model))
            .ToList();

        var embeddingsJsonlBytes = RagExportSerializer.ToJsonlBytes(embeddingLines);
        await _storage.WriteFileAsync($"{basePath}/embeddings.jsonl", embeddingsJsonlBytes, ct).ConfigureAwait(false);

        var embeddingsParquetBytes = await RagParquetSerializer.SerializeEmbeddingsAsync(embeddingLines).ConfigureAwait(false);
        await _storage.WriteFileAsync($"{basePath}/embeddings.parquet", embeddingsParquetBytes, ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Exported document bundle: pdfDocumentId={PdfDocumentId} game={GameSlug} " +
            "chunks={ChunkCount} embeddings={EmbeddingCount} path={BasePath}",
            pdfDoc.Id, gameSlug, chunkLines.Count, embeddingLines.Count, basePath);
    }

    /// <inheritdoc/>
    public async Task WriteManifestAsync(
        string snapshotId,
        RagExportManifest manifest,
        CancellationToken ct = default)
    {
        var path = $"rag-exports/{snapshotId}/manifest.json";
        var bytes = RagExportSerializer.ToJsonBytes(manifest);
        await _storage.WriteFileAsync(path, bytes, ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Wrote manifest: snapshotId={SnapshotId} documents={DocumentCount}",
            snapshotId, manifest.TotalDocuments);
    }

    /// <inheritdoc/>
    public async Task<RagExportManifest?> ReadManifestAsync(
        string snapshotId,
        CancellationToken ct = default)
    {
        var path = $"rag-exports/{snapshotId}/manifest.json";
        var bytes = await _storage.ReadFileAsync(path, ct).ConfigureAwait(false);

        if (bytes is null)
            return null;

        return JsonSerializer.Deserialize<RagExportManifest>(bytes);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static RagExportDocumentMetadata BuildDocumentMetadata(
        VectorDocumentEntity vectorDoc,
        PdfDocumentEntity pdfDoc,
        string gameName,
        string gameSlug,
        List<TextChunkEntity> chunks,
        List<PgVectorEmbeddingEntity> embeddings)
    {
        var totalChars = chunks.Sum(c => c.CharacterCount);
        var avgChunkSize = chunks.Count > 0 ? totalChars / chunks.Count : 0;

        var docInfo = new RagExportDocumentInfo(
            PdfDocumentId: pdfDoc.Id,
            GameId: pdfDoc.GameId,
            GameSlug: gameSlug,
            GameName: gameName,
            FileName: pdfDoc.FileName,
            Language: pdfDoc.Language,
            LanguageConfidence: pdfDoc.LanguageConfidence,
            DocumentCategory: pdfDoc.DocumentCategory,
            VersionLabel: pdfDoc.VersionLabel,
            LicenseType: pdfDoc.LicenseType,
            PageCount: pdfDoc.PageCount,
            CharacterCount: pdfDoc.CharacterCount,
            ContentHash: pdfDoc.ContentHash,
            FileSizeBytes: pdfDoc.FileSizeBytes);

        var stats = new RagExportDocumentStats(
            TotalChunks: chunks.Count,
            TotalEmbeddings: embeddings.Count,
            ChunkSizeAvg: avgChunkSize);

        return new RagExportDocumentMetadata(
            ExportVersion: ExportVersion,
            ExportedAt: DateTimeOffset.UtcNow,
            EmbeddingModel: vectorDoc.EmbeddingModel,
            EmbeddingDimensions: vectorDoc.EmbeddingDimensions,
            Document: docInfo,
            Stats: stats);
    }
}

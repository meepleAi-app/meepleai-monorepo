using System.Text.Json;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace Api.BoundedContexts.Administration.Application.Commands.ImportRagData;

/// <summary>
/// Handles importing RAG data from a backup snapshot into the database.
/// Reads manifest.json, matches games by slug, deduplicates by ContentHash,
/// and creates PdfDocumentEntity + VectorDocumentEntity + TextChunkEntity records.
/// Optionally imports stored embeddings (PgVectorEmbeddingEntity) when ReEmbed=false.
/// </summary>
internal sealed class ImportRagDataCommandHandler : IRequestHandler<ImportRagDataCommand, ImportRagDataResult>
{
    private readonly MeepleAiDbContext _db;
    private readonly IRagBackupStorageService _storage;
    private readonly ILogger<ImportRagDataCommandHandler> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ImportRagDataCommandHandler(
        MeepleAiDbContext db,
        IRagBackupStorageService storage,
        ILogger<ImportRagDataCommandHandler> logger)
    {
        _db = db;
        _storage = storage;
        _logger = logger;
    }

    public async Task<ImportRagDataResult> Handle(ImportRagDataCommand request, CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        var errors = new List<string>();
        var imported = 0;
        var skipped = 0;
        var failed = 0;
        var reEmbedded = 0;

        // ── 1. Read manifest ──────────────────────────────────────────────────
        var manifestPath = $"{request.SnapshotPath}/manifest.json";
        var manifestBytes = await _storage.ReadFileAsync(manifestPath, cancellationToken).ConfigureAwait(false);

        if (manifestBytes is null)
        {
            _logger.LogError("Manifest not found at {Path}", manifestPath);
            return new ImportRagDataResult(
                TotalDocuments: 0,
                Imported: 0,
                Skipped: 0,
                Failed: 0,
                ReEmbedded: 0,
                Warnings: warnings,
                Errors: [$"Manifest not found at path: {manifestPath}"]);
        }

        RagExportManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<RagExportManifest>(manifestBytes, JsonOptions)
                       ?? throw new InvalidOperationException("Manifest deserialized to null");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize manifest at {Path}", manifestPath);
            return new ImportRagDataResult(
                TotalDocuments: 0,
                Imported: 0,
                Skipped: 0,
                Failed: 0,
                ReEmbedded: 0,
                Warnings: warnings,
                Errors: [$"Failed to deserialize manifest: {ex.Message}"]);
        }

        _logger.LogInformation(
            "ImportRagData: manifest loaded, {Count} documents, snapshotPath={SnapshotPath}",
            manifest.TotalDocuments, request.SnapshotPath);

        // ── 2. Build game slug → GameEntity lookup ────────────────────────────
        var allGames = await _db.Games
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var gameBySlug = allGames
            .GroupBy(g => RagBackupPathHelper.Slugify(g.Name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // ── 3. Import each document ───────────────────────────────────────────
        foreach (var entry in manifest.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();

#pragma warning disable CA1031 // Import must continue on individual document errors
            try
            {
                // a. Match game by slug
                if (!gameBySlug.TryGetValue(entry.GameSlug, out var game))
                {
                    warnings.Add($"Game slug '{entry.GameSlug}' not found in database — skipping document {entry.PdfDocumentId}");
                    _logger.LogWarning(
                        "ImportRagData: skipping document {PdfDocumentId} — game slug '{GameSlug}' not found",
                        entry.PdfDocumentId, entry.GameSlug);
                    skipped++;
                    continue;
                }

                // b. Read document metadata.json
                var metadataPath = $"{entry.Path}/metadata.json";
                var metadataBytes = await _storage.ReadFileAsync(metadataPath, cancellationToken).ConfigureAwait(false);

                if (metadataBytes is null)
                {
                    warnings.Add($"metadata.json not found for document {entry.PdfDocumentId} at {metadataPath} — skipping");
                    skipped++;
                    continue;
                }

                var metadata = JsonSerializer.Deserialize<RagExportDocumentMetadata>(metadataBytes, JsonOptions);
                if (metadata is null)
                {
                    warnings.Add($"metadata.json deserialized to null for document {entry.PdfDocumentId} — skipping");
                    skipped++;
                    continue;
                }

                var docInfo = metadata.Document;

                // c. Duplicate check by ContentHash
                if (!string.IsNullOrEmpty(docInfo.ContentHash))
                {
                    var exists = await _db.PdfDocuments
                        .AnyAsync(p => p.ContentHash == docInfo.ContentHash, cancellationToken)
                        .ConfigureAwait(false);

                    if (exists)
                    {
                        _logger.LogInformation(
                            "ImportRagData: skipping document {PdfDocumentId} — ContentHash already exists",
                            entry.PdfDocumentId);
                        skipped++;
                        continue;
                    }
                }

                // d. Read chunks.parquet
                var chunksPath = $"{entry.Path}/chunks.parquet";
                var chunksBytes = await _storage.ReadFileAsync(chunksPath, cancellationToken).ConfigureAwait(false);

                if (chunksBytes is null)
                {
                    warnings.Add($"chunks.parquet not found for document {entry.PdfDocumentId} — skipping");
                    skipped++;
                    continue;
                }

                var chunks = await RagParquetSerializer.DeserializeChunksAsync(chunksBytes).ConfigureAwait(false);

                // e. Create PdfDocumentEntity
                var pdfDocumentId = Guid.NewGuid();
                var pdfDocument = new PdfDocumentEntity
                {
                    Id = pdfDocumentId,
                    GameId = game.Id,
                    FileName = docInfo.FileName,
                    FilePath = string.Empty,
                    FileSizeBytes = docInfo.FileSizeBytes ?? 0,
                    Language = docInfo.Language,
                    LanguageConfidence = docInfo.LanguageConfidence,
                    DocumentCategory = docInfo.DocumentCategory ?? "Rulebook",
                    VersionLabel = docInfo.VersionLabel,
                    LicenseType = docInfo.LicenseType ?? 0,
                    PageCount = docInfo.PageCount,
                    CharacterCount = docInfo.CharacterCount,
                    ContentHash = docInfo.ContentHash,
                    ProcessingState = "Ready",
                    IsActiveForRag = true,
                    UploadedAt = DateTime.UtcNow,
                    IsPublic = true,
                };

                _db.PdfDocuments.Add(pdfDocument);

                // f. Create VectorDocumentEntity
                var vectorDocumentId = Guid.NewGuid();
                var vectorDocument = new VectorDocumentEntity
                {
                    Id = vectorDocumentId,
                    GameId = game.Id,
                    PdfDocumentId = pdfDocumentId,
                    ChunkCount = chunks.Count,
#pragma warning disable S125
                    // When ReEmbed=true, mark as pending so the indexing pipeline picks it up;
                    // otherwise mark as completed because we are importing the stored embeddings.
#pragma warning restore S125
                    IndexingStatus = request.ReEmbed ? "pending" : "completed",
                    IndexedAt = DateTime.UtcNow,
                    EmbeddingModel = metadata.EmbeddingModel,
                    EmbeddingDimensions = metadata.EmbeddingDimensions,
                };

                _db.VectorDocuments.Add(vectorDocument);

                // g. Create TextChunkEntity for each chunk
                foreach (var chunk in chunks)
                {
                    var textChunk = new TextChunkEntity
                    {
                        Id = Guid.NewGuid(),
                        GameId = game.Id,
                        PdfDocumentId = pdfDocumentId,
                        Content = chunk.Content,
                        ChunkIndex = chunk.ChunkIndex,
                        PageNumber = chunk.PageNumber,
                        CharacterCount = chunk.CharacterCount,
                        CreatedAt = DateTime.UtcNow,
                    };

                    _db.TextChunks.Add(textChunk);
                }

                // h/i. Embeddings: import or mark for re-embed
                if (!request.ReEmbed)
                {
                    var embeddingsPath = $"{entry.Path}/embeddings.parquet";
                    var embeddingsBytes = await _storage.ReadFileAsync(embeddingsPath, cancellationToken).ConfigureAwait(false);

                    if (embeddingsBytes is not null)
                    {
                        var embeddings = await RagParquetSerializer.DeserializeEmbeddingsAsync(embeddingsBytes).ConfigureAwait(false);

                        foreach (var embedding in embeddings)
                        {
                            var pgEmbedding = new PgVectorEmbeddingEntity
                            {
                                Id = Guid.NewGuid(),
                                VectorDocumentId = vectorDocumentId,
                                GameId = game.Id,
                                TextContent = embedding.TextContent,
                                Vector = new Vector(embedding.Vector),
                                Model = embedding.Model,
                                ChunkIndex = embedding.ChunkIndex,
                                PageNumber = embedding.PageNumber ?? 0,
                                Lang = docInfo.Language,
                                CreatedAt = DateTimeOffset.UtcNow,
                            };

                            _db.PgVectorEmbeddings.Add(pgEmbedding);
                        }
                    }
                    else
                    {
                        warnings.Add($"embeddings.parquet not found for document {entry.PdfDocumentId} — embeddings skipped");
                    }
                }
                else
                {
                    warnings.Add($"Document {entry.PdfDocumentId} ('{entry.GameSlug}'): re-embedding required post-import");
                    reEmbedded++;
                }

                // j. Save per document
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation(
                    "ImportRagData: imported document {PdfDocumentId} for game '{GameSlug}' ({ChunkCount} chunks)",
                    pdfDocumentId, entry.GameSlug, chunks.Count);

                imported++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors.Add($"Error importing document {entry.PdfDocumentId}: {ex.Message}");
                failed++;
                _logger.LogWarning(ex, "ImportRagData: failed to import document {PdfDocumentId}", entry.PdfDocumentId);
                // Clear any partially-tracked entities so they do not corrupt the next iteration.
                _db.ChangeTracker.Clear();
            }
#pragma warning restore CA1031
        }

        _logger.LogInformation(
            "ImportRagData complete: total={Total} imported={Imported} skipped={Skipped} failed={Failed} reEmbedded={ReEmbedded}",
            manifest.TotalDocuments, imported, skipped, failed, reEmbedded);

        return new ImportRagDataResult(
            TotalDocuments: manifest.TotalDocuments,
            Imported: imported,
            Skipped: skipped,
            Failed: failed,
            ReEmbedded: reEmbedded,
            Warnings: warnings,
            Errors: errors);
    }
}

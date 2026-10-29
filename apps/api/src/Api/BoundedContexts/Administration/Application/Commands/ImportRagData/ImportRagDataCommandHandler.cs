using System.Text.Json;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Services;
using Api.Constants;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Commands.ImportRagData;

/// <summary>
/// Handles importing RAG data from a backup snapshot into the database.
/// Reads manifest.json, matches games by slug, deduplicates by ContentHash,
/// and creates PdfDocumentEntity + VectorDocumentEntity + TextChunkEntity records.
/// Imports the stored embeddings (PgVectorEmbeddingEntity) when ReEmbed=false; with ReEmbed=true it ignores them
/// and embeds the imported chunks with the current model instead (#4725).
/// </summary>
internal sealed class ImportRagDataCommandHandler : IRequestHandler<ImportRagDataCommand, ImportRagDataResult>
{
    private readonly MeepleAiDbContext _db;
    private readonly IRagBackupStorageService _storage;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<ImportRagDataCommandHandler> _logger;

    /// <summary>Texts per call to the embedding service when re-embedding: small, as in <c>VectorReembeddingService</c> (sequential Ollama).</summary>
    private const int EmbeddingBatchSize = 10;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ImportRagDataCommandHandler(
        MeepleAiDbContext db,
        IRagBackupStorageService storage,
        IEmbeddingService embeddingService,
        ILogger<ImportRagDataCommandHandler> logger)
    {
        _db = db;
        _storage = storage;
        _embeddingService = embeddingService;
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
            // SECURITY (test-guarded): expose only the exception TYPE in the returned
            // payload. JsonException.Message includes "Path: $.<property> | LineNumber:
            // N | BytePositionInLine: N" — leaking the manifest schema and file layout
            // to admin API callers and log shipping pipelines. The full message + stack
            // trace are preserved server-side by LogError above. The unit test
            // ImportRagDataCommandHandlerTests.Handle_WhenManifestDeserializationFails_ShouldNotIncludeRawExceptionMessageInError
            // enforces this contract.
            return new ImportRagDataResult(
                TotalDocuments: 0,
                Imported: 0,
                Skipped: 0,
                Failed: 0,
                ReEmbedded: 0,
                Warnings: warnings,
                Errors: [$"Failed to deserialize manifest ({ex.GetType().Name})"]);
        }

        _logger.LogInformation(
            "ImportRagData: manifest loaded, {Count} documents, snapshotPath={SnapshotPath}",
            manifest.TotalDocuments, request.SnapshotPath);

        // ── 2. Build game slug → GameEntity lookup ────────────────────────────
        var allGames = await _db.SharedGames
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var gameBySlug = allGames
            .GroupBy(g => RagBackupPathHelper.Slugify(g.Title), StringComparer.OrdinalIgnoreCase)
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

                // Post-Phase2d: SharedGame.Id is non-nullable; PdfDocument always gets a SharedGameId.
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
                    // Guaranteed non-null by guard above (game.Id is not null).
                    SharedGameId = game.Id,
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
#pragma warning disable MAI005 // Out-of-scope for the #2244 / P234 refactor — admin RAG data import
                                // is a separate ingestion path that bulk-imports a backup: its stored embeddings, or
                                // (ReEmbed) ones recomputed from the imported chunks (no domain event needed since
                                // this is import, not new indexing). Migrate to
                                // domain.ToEntity() pattern if/when this path needs to raise events.
                var vectorDocument = new VectorDocumentEntity
                {
                    Id = vectorDocumentId,
                    GameId = game.Id,
                    PdfDocumentId = pdfDocumentId,
                    ChunkCount = chunks.Count,
                    // Completed either way: the embeddings are imported, or (ReEmbed) computed below before the save,
                    // so this row never exists without them. It used to be "pending" for ReEmbed, and nothing
                    // picked a pending row up (#4725). With ReEmbed the document describes the embeddings it
                    // really has, the current model's, not the backup's.
                    IndexingStatus = "completed",
                    IndexedAt = DateTime.UtcNow,
                    EmbeddingModel = request.ReEmbed ? _embeddingService.GetModelName() : metadata.EmbeddingModel,
                    EmbeddingDimensions = request.ReEmbed ? _embeddingService.GetEmbeddingDimensions() : metadata.EmbeddingDimensions,
                };
#pragma warning restore MAI005

                _db.VectorDocuments.Add(vectorDocument);

                // g. Create TextChunkEntity for each chunk.
                // #4384: kept by ChunkIndex so each imported pgvector row can point at its chunk.
                var textChunks = new List<TextChunkEntity>(chunks.Count);
                var textChunksByIndex = new Dictionary<int, TextChunkEntity>();
                var ambiguousIndexes = new HashSet<int>();
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
                    textChunks.Add(textChunk);
                    if (!textChunksByIndex.TryAdd(chunk.ChunkIndex, textChunk))
                    {
                        // Ambiguous: two chunks on one index. No embedding links to either of them.
                        ambiguousIndexes.Add(chunk.ChunkIndex);
                    }
                }

                foreach (var index in ambiguousIndexes)
                {
                    textChunksByIndex.Remove(index);
                }

                // Only imported embeddings arrive with just an index; re-embedded ones come from one chunk's text.
                if (ambiguousIndexes.Count > 0 && !request.ReEmbed)
                {
                    warnings.Add($"Document {entry.PdfDocumentId}: {ambiguousIndexes.Count} chunk index(es) appear more than once in chunks.parquet — their embeddings are imported without a link to a chunk");
                }

                // h/i. Embeddings: import or mark for re-embed
                if (!request.ReEmbed)
                {
                    var embeddingsPath = $"{entry.Path}/embeddings.parquet";
                    var embeddingsBytes = await _storage.ReadFileAsync(embeddingsPath, cancellationToken).ConfigureAwait(false);

                    if (embeddingsBytes is not null)
                    {
                        var embeddings = await RagParquetSerializer.DeserializeEmbeddingsAsync(embeddingsBytes).ConfigureAwait(false);

                        var unlinked = 0;
                        foreach (var embedding in embeddings)
                        {
                            var link = textChunksByIndex.TryGetValue(embedding.ChunkIndex, out var textChunk)
                                ? TextChunkLink.From(textChunk)
                                : null;
                            if (link is null)
                            {
                                unlinked++;
                            }

                            var pgEmbedding = PgVectorEmbeddingEntity.ForChunk(
                                vectorDocumentId: vectorDocumentId,
                                gameId: game.Id,
                                chunkIndex: embedding.ChunkIndex,
                                textContent: embedding.TextContent,
                                vector: embedding.Vector,
                                model: embedding.Model,
                                pageNumber: embedding.PageNumber ?? 0,
                                lang: docInfo.Language,
                                sourceChunk: link);

                            _db.PgVectorEmbeddings.Add(pgEmbedding);
                        }

                        if (unlinked > 0)
                        {
                            warnings.Add($"Document {entry.PdfDocumentId}: {unlinked} embedding(s) have no unambiguous chunk with the same index — imported without a link (no heading boost, no citation location)");
                        }
                    }
                    else
                    {
                        warnings.Add($"embeddings.parquet not found for document {entry.PdfDocumentId} — embeddings skipped");
                    }
                }
                else
                {
                    // #4725: the text is already here (chunks.parquet became text_chunks above), so the embeddings are
                    // computed from it now, before the save: the document enters complete or not at all. A failure of
                    // the embedding service ends in the catch below, which drops everything tracked for this document.
                    var vectors = await EmbedChunksAsync(chunks, cancellationToken).ConfigureAwait(false);
                    var model = _embeddingService.GetModelName();

                    for (var i = 0; i < chunks.Count; i++)
                    {
                        _db.PgVectorEmbeddings.Add(PgVectorEmbeddingEntity.ForChunk(
                            vectorDocumentId: vectorDocumentId,
                            gameId: game.Id,
                            chunkIndex: chunks[i].ChunkIndex,
                            textContent: chunks[i].Content,
                            vector: vectors[i],
                            model: model,
                            pageNumber: Math.Max(1, chunks[i].PageNumber ?? 1),
                            lang: docInfo.Language,
                            sourceChunk: TextChunkLink.From(textChunks[i])));
                    }
                }

                // j. Save per document
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation(
                    "ImportRagData: imported document {PdfDocumentId} for game '{GameSlug}' ({ChunkCount} chunks)",
                    pdfDocumentId, entry.GameSlug, chunks.Count);

                imported++;
                if (request.ReEmbed)
                {
                    reEmbedded++;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // SECURITY (test-guarded): never embed ex.Message in the per-document
                // error. EF Core / storage exceptions routinely carry SQL fragments,
                // parameter values, and backend-internal details (bucket names, request
                // IDs, etc.). Surface only the exception type; full diagnostic detail
                // is preserved by the LogWarning below. Enforced by
                // ImportRagDataCommandHandlerTests.Handle_WhenStorageReadDuringImportThrows_ShouldNotIncludeRawExceptionMessageInError.
                errors.Add($"Error importing document {entry.PdfDocumentId} ({ex.GetType().Name})");
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

    /// <summary>
    /// One vector per chunk, in the chunks' order, from the current embedding model. Throws if the service fails,
    /// answers with a different number of vectors, or returns a vector the index would refuse: a half-embedded
    /// document must not be saved.
    /// </summary>
    private async Task<List<float[]>> EmbedChunksAsync(IReadOnlyList<RagExportChunkLine> chunks, CancellationToken cancellationToken)
    {
        var dimensions = _embeddingService.GetEmbeddingDimensions();
        var vectors = new List<float[]>(chunks.Count);

        for (var offset = 0; offset < chunks.Count; offset += EmbeddingBatchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Only the provider input is capped, as in every ingest path: a parent chunk is a whole section and
            // can be longer than the provider accepts. The full text stays in text_chunks and in the embedding row.
            var texts = chunks.Skip(offset).Take(EmbeddingBatchSize).Select(c => CapForEmbedding(c.Content)).ToList();
            var batch = await _embeddingService.GenerateEmbeddingsAsync(texts, cancellationToken).ConfigureAwait(false);

            // EmbeddingService turns every provider exception, cancellation included, into a failed result: without
            // this line a cancelled import would be recorded as a failed document and carry on with the next one.
            cancellationToken.ThrowIfCancellationRequested();

            if (!batch.Success || batch.Embeddings.Count != texts.Count)
            {
                throw new InvalidOperationException(
                    $"Embedding generation failed for chunks {offset}-{offset + texts.Count - 1}: " +
                    (batch.Success ? $"{batch.Embeddings.Count} vectors for {texts.Count} texts" : batch.ErrorMessage));
            }

            // The width is the model's: a fallback provider answering with another one would mix models in a
            // document labelled with the primary's, and pgvector refuses empty or non-finite vectors only at the save.
            if (batch.Embeddings.Exists(v => v is null || v.Length != dimensions || Array.Exists(v, x => !float.IsFinite(x))))
            {
                throw new InvalidOperationException(
                    $"Invalid embedding vector for chunks {offset}-{offset + texts.Count - 1} (expected {dimensions} finite values)");
            }

            vectors.AddRange(batch.Embeddings);
        }

        return vectors;
    }

    private static string CapForEmbedding(string text) =>
        text.Length <= ChunkingConstants.MaxEmbeddingChars ? text : text[..ChunkingConstants.MaxEmbeddingChars];
}

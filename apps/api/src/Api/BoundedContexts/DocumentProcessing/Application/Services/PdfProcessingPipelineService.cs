using Api.BoundedContexts.DocumentProcessing.Application.Services.Chunking;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Observability;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Covers;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using KbEntities = Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using KbValueObjects = Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Shared PDF processing pipeline: extract → chunk → embed → index.
/// Used by the stale PDF recovery job and can be adopted by upload handlers.
/// </summary>
internal sealed class PdfProcessingPipelineService : IPdfProcessingPipelineService
{
    private const int ChunkSize = 1024;
    private const int ChunkOverlap = 150;
    private const int EmbeddingBatchSize = 20;

    private readonly MeepleAiDbContext _db;
    private readonly IPdfClaimService _pdfClaimService;
    private readonly IPdfTextExtractor _pdfTextExtractor;
    private readonly IPdfTableExtractor _tableExtractor;
    private readonly ITextChunkingService _chunkingService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IBlobStorageService _blobStorageService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PdfProcessingPipelineService> _logger;
    private readonly IRaptorIndexer? _raptorIndexer;
    private readonly IEntityExtractor? _entityExtractor;
    private readonly IVectorStoreAdapter? _vectorStore;
    private readonly IFeatureFlagService? _featureFlagService;
    private readonly ILanguageDetector _languageDetector;
    private readonly IChunkTranslationService _chunkTranslationService;
    // Phase D4 (gamebook multi-book): optional role classifier for tagging chunks at ingest.
    // Optional so unit tests that pre-date Phase D continue to compile without updates.
    private readonly IRoleClassifierService? _roleClassifier;
    // Issue #1831 (umbrella #1821 L4): optional cover extractor — pipeline does
    // NOT fail if cover generation throws; we just mark the row as Failed and
    // continue (the L1 placeholder remains visible client-side).
    private readonly IPdfCoverExtractor? _pdfCoverExtractor;
    // Issue #1852 (Gap A): collects PdfCoverGeneratedEvent for dispatch at next
    // SaveChangesAsync so the SharedGame.PdfCoverR2Key column is populated.
    // Nullable so pre-#1852 test constructors compile without adding a new mock param.
    private readonly IDomainEventCollector? _eventCollector;

    // Issue #2947: deterministic R2 cover writes. Optional so pre-#2947 unit-test
    // constructors compile; when null, cover generation is skipped like when
    // _pdfCoverExtractor is null.
    private readonly IPdfCoverUploadPipeline? _pdfCoverUploadPipeline;

    private readonly IPdfIndexingPipeline _indexingPipeline;

    // Issue #3281: optional so pre-existing test constructors compile. When null,
    // chunk production falls back to the flat ITextChunkingService path (pre-Slice-D behaviour).
    private readonly IAdvancedChunkingService? _advancedChunking;

    public PdfProcessingPipelineService(
        MeepleAiDbContext db,
        IPdfClaimService pdfClaimService,
        IPdfTextExtractor pdfTextExtractor,
        IPdfTableExtractor tableExtractor,
        ITextChunkingService chunkingService,
        IEmbeddingService embeddingService,
        IBlobStorageService blobStorageService,
        TimeProvider timeProvider,
        ILogger<PdfProcessingPipelineService> logger,
        ILanguageDetector languageDetector,
        IChunkTranslationService chunkTranslationService,
        IPdfIndexingPipeline indexingPipeline,
        IRaptorIndexer? raptorIndexer = null,
        IEntityExtractor? entityExtractor = null,
        IVectorStoreAdapter? vectorStore = null,
        IFeatureFlagService? featureFlagService = null,
        IRoleClassifierService? roleClassifier = null,
        IPdfCoverExtractor? pdfCoverExtractor = null,
        IDomainEventCollector? eventCollector = null,
        IPdfCoverUploadPipeline? pdfCoverUploadPipeline = null,
        IAdvancedChunkingService? advancedChunking = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _pdfClaimService = pdfClaimService ?? throw new ArgumentNullException(nameof(pdfClaimService));
        _pdfTextExtractor = pdfTextExtractor ?? throw new ArgumentNullException(nameof(pdfTextExtractor));
        _tableExtractor = tableExtractor ?? throw new ArgumentNullException(nameof(tableExtractor));
        _chunkingService = chunkingService ?? throw new ArgumentNullException(nameof(chunkingService));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _blobStorageService = blobStorageService ?? throw new ArgumentNullException(nameof(blobStorageService));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _languageDetector = languageDetector ?? throw new ArgumentNullException(nameof(languageDetector));
        _chunkTranslationService = chunkTranslationService ?? throw new ArgumentNullException(nameof(chunkTranslationService));
        _indexingPipeline = indexingPipeline ?? throw new ArgumentNullException(nameof(indexingPipeline));
        _raptorIndexer = raptorIndexer;
        _entityExtractor = entityExtractor;
        _vectorStore = vectorStore;
        _featureFlagService = featureFlagService;
        _roleClassifier = roleClassifier;
        _pdfCoverExtractor = pdfCoverExtractor;
        // eventCollector is optional so pre-#1852 test constructors continue
        // to compile without updating every mock site. The Collect() call
        // in ExtractCoverImageAsync is guarded with a null-check.
        _eventCollector = eventCollector;
        _pdfCoverUploadPipeline = pdfCoverUploadPipeline;
        _advancedChunking = advancedChunking;
    }

    public async Task ProcessAsync(
        Guid pdfDocumentId,
        string filePath,
        Guid uploadedByUserId,
        CancellationToken cancellationToken)
    {
        var pdfId = pdfDocumentId.ToString();

        _logger.LogInformation("[PdfPipeline] Starting processing for PDF {PdfId}", pdfId);

        try
        {
            // Atomic claim: transition Pending → Extracting in a single operation.
            // Issue #892: extracted to IPdfClaimService — production uses raw SQL UPDATE
            // (RelationalPdfClaimService) for atomic guarantees under contention; tests
            // inject InMemoryPdfClaimService which uses tracked Find + SaveChanges.
            // Stuck-state recovery is RetryFailedPdfsJob's responsibility, not the claim.
            var claimed = await _pdfClaimService.TryClaimPendingAsync(pdfDocumentId, cancellationToken).ConfigureAwait(false);
            if (!claimed)
            {
                _logger.LogInformation(
                    "[PdfPipeline] PDF {PdfId} not in Pending state (already claimed or terminal), skipping",
                    pdfId);
                return;
            }

            // Re-load with tracked entity for the rest of the pipeline.
            var pdfDoc = await _db.PdfDocuments
                .FindAsync(new object[] { pdfDocumentId }, cancellationToken)
                .ConfigureAwait(false);

            if (pdfDoc == null)
            {
                _logger.LogError("[PdfPipeline] PDF document {PdfId} disappeared after claim", pdfId);
                return;
            }
            // Refresh tracked entity to reflect the UPDATE we just executed.
            await _db.Entry(pdfDoc).ReloadAsync(cancellationToken).ConfigureAwait(false);

            // Step 1: Extract text
            _logger.LogInformation("[PdfPipeline] Step 1/4: Extracting text from {PdfId}", pdfId);
            var (fullText, extractResult) = await ExtractTextAsync(pdfDoc, filePath, cancellationToken).ConfigureAwait(false);

            // Step 2: Extract structured content (tables)
            _logger.LogInformation("[PdfPipeline] Step 2/4: Extracting structured content from {PdfId}", pdfId);
            await ExtractStructuredContentAsync(pdfDoc, filePath, cancellationToken).ConfigureAwait(false);

            // Detect document language (Issue: RAG retrieval quality)
            var langResult = _languageDetector.Detect(fullText);
            pdfDoc.Language = langResult.DetectedLanguage;
            pdfDoc.LanguageConfidence = langResult.Confidence;
            _logger.LogInformation(
                "[PdfPipeline] Detected language: {Language} (confidence: {Confidence:F2}) for PDF {PdfId}",
                langResult.DetectedLanguage, langResult.Confidence, pdfDoc.Id);

            // Issue #1831 (L4): extract first-page cover image. Best-effort —
            // failures are logged on the entity and do not block the pipeline.
            await ExtractCoverImageAsync(pdfDoc, filePath, cancellationToken).ConfigureAwait(false);

            // Issue #4215: Transition to Chunking state
            pdfDoc.ProcessingState = nameof(PdfProcessingState.Chunking);
            try
            {
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                MeepleAiMetrics.RecordPdfConcurrencyConflict(
                    nameof(PdfProcessingPipelineService),
                    MeepleAiMetrics.PdfConcurrencyCategories.B);
                _logger.LogWarning(ex,
                    "Concurrency conflict on PdfDocument {PdfId} in {Handler} (Category B) — admin mutation wins, pipeline will re-read on next tick",
                    pdfId, nameof(PdfProcessingPipelineService));
                return; // CRITICAL: do not throw — Quartz must see job as successful
            }

            // Step 3: Chunk text
            _logger.LogInformation("[PdfPipeline] Step 3/4: Chunking text for {PdfId} ({CharCount} chars)", pdfId, fullText.Length);
            var chunks = await ChunkTextAsync(
                fullText,
                extractResult,
                pdfDoc.Id,
                pdfDoc.PrivateGameId ?? pdfDoc.SharedGameId,
                cancellationToken).ConfigureAwait(false);

            if (chunks.Count == 0)
            {
                _logger.LogWarning("[PdfPipeline] No chunks produced for {PdfId}, marking as failed", pdfId);
                await MarkFailedAsync(pdfDoc, "Text extraction produced no usable chunks").ConfigureAwait(false);
                return;
            }

            // Dual-language indexing: translate non-English chunks to English
            var detectedLang = pdfDoc.Language ?? "en";
            var translatedChunks = new List<(DocumentChunkInput chunk, string lang, bool isTranslation)>();

            // Add all original chunks with their detected language
            foreach (var chunk in chunks)
                translatedChunks.Add((chunk, detectedLang, false));

            if (!string.Equals(detectedLang, "en", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation(
                    "[PdfPipeline] Translating {ChunkCount} chunks from {Lang} to EN for PDF {PdfId}",
                    chunks.Count, detectedLang, pdfDoc.Id);

                try
                {
                    var translations = await _chunkTranslationService.TranslateChunksAsync(
                        chunks.Select(c => c.Text).ToList(),
                        detectedLang, "en", cancellationToken).ConfigureAwait(false);

                    foreach (var t in translations)
                    {
                        if (!string.IsNullOrWhiteSpace(t.TranslatedText))
                        {
                            var origChunk = chunks[t.OriginalIndex];
                            translatedChunks.Add((
                                new DocumentChunkInput
                                {
                                    // Id intentionally omitted → defaults to Guid.Empty → fresh Guid at persist.
                                    // Copying origChunk.Id here duplicates a primary key and fails non-English PDFs.
                                    Text = t.TranslatedText,
                                    Page = origChunk.Page,
                                    CharStart = origChunk.CharStart,
                                    CharEnd = origChunk.CharEnd,
                                    Heading = origChunk.Heading,
                                    Level = origChunk.Level,
                                    ParentChunkId = origChunk.ParentChunkId,
                                    ElementType = origChunk.ElementType
                                },
                                "en",
                                true));
                        }
                    }

                    _logger.LogInformation(
                        "[PdfPipeline] Added {TranslatedCount} EN translations for PDF {PdfId}",
                        translations.Count, pdfDoc.Id);
                }
#pragma warning disable CA1031 // Translation is optional, must not block pipeline
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "[PdfPipeline] Translation failed for PDF {PdfId}, proceeding with original chunks only",
                        pdfDoc.Id);
                }
#pragma warning restore CA1031
            }

            // RAPTOR.RaptorSummaries and GraphRAG.GameEntityRelations both have FK
            // on games.Id. For PDFs uploaded against a SharedGame, resolve the
            // matching games row via PdfGameIdResolver (returns null if no
            // games-table peer exists). Naively using pdfDoc.SharedGameId
            // produces FK violations because shared_games.id ≠ games.Id.
            var raptorGameId = await PdfGameIdResolver
                .ResolveAsync(_db, pdfDoc, cancellationToken)
                .ConfigureAwait(false);

            // === RAPTOR: Build hierarchical summary tree (optional, non-blocking) ===
            // Check if raptor-retrieval enhancement is globally enabled before spending LLM tokens
            var raptorEnabled = _featureFlagService != null
                && await _featureFlagService.IsEnabledAsync("rag.enhancement.raptor-retrieval").ConfigureAwait(false);

            if (raptorEnabled && _raptorIndexer != null && raptorGameId is null)
            {
                _logger.LogInformation(
                    "[PdfPipeline] Skipping RAPTOR for PDF {PdfId}: no games-table peer (SharedGameId={SharedId}, PrivateGameId={PrivateId})",
                    pdfDoc.Id, pdfDoc.SharedGameId, pdfDoc.PrivateGameId);
            }
            else if (_raptorIndexer != null && raptorEnabled && chunks.Count > 3 && raptorGameId is { } gid)
            {
                try
                {
                    var chunkTexts = chunks.Select(c => c.Text).ToList();
                    var raptorResult = await _raptorIndexer.BuildTreeAsync(
                        pdfDoc.Id, gid,
                        chunkTexts, maxLevels: 3, cancellationToken).ConfigureAwait(false);

                    if (raptorResult.TotalNodes > 0)
                    {
                        await SaveRaptorSummariesAsync(
                            pdfDoc.Id, gid,
                            raptorResult.Summaries, cancellationToken).ConfigureAwait(false);

                        _logger.LogInformation(
                            "[PdfPipeline] RAPTOR: built {Levels}-level tree with {Nodes} summary nodes for PDF {PdfId}",
                            raptorResult.Levels, raptorResult.TotalNodes, pdfDoc.Id);
                    }
                }
#pragma warning disable CA1031 // RAPTOR is optional enhancement, must not block pipeline
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "[PdfPipeline] RAPTOR indexing failed for PDF {PdfId}, continuing without hierarchical summaries",
                        pdfDoc.Id);
                    DetachUnsavedChanges<RaptorSummaryEntity>();
                }
#pragma warning restore CA1031
            }

            // === Graph RAG: Extract entity relations (optional, non-blocking) ===
            // Check if graph-traversal enhancement is globally enabled before spending LLM tokens
            var graphRagEnabled = _featureFlagService != null
                && await _featureFlagService.IsEnabledAsync("rag.enhancement.graph-traversal").ConfigureAwait(false);

            if (graphRagEnabled && _entityExtractor is not null && raptorGameId is null)
            {
                _logger.LogInformation(
                    "[PdfPipeline] Skipping Graph RAG for PDF {PdfId}: no games-table peer",
                    pdfDoc.Id);
            }
            else if (_entityExtractor is not null && graphRagEnabled && fullText.Length >= 200 && raptorGameId is { } graphGid)
            {
                try
                {
                    var gameTitle = pdfDoc.FileName ?? "Unknown";
                    var extraction = await _entityExtractor.ExtractEntitiesAsync(
                        graphGid, gameTitle,
                        fullText[..Math.Min(fullText.Length, 8000)],
                        cancellationToken).ConfigureAwait(false);

                    if (extraction.Relations.Count > 0)
                    {
                        var entities = extraction.Relations.Select(r => new GameEntityRelationEntity
                        {
                            Id = Guid.NewGuid(),
                            GameId = graphGid,
                            SourceEntity = r.SourceEntity,
                            SourceType = r.SourceType,
                            Relation = r.Relation,
                            TargetEntity = r.TargetEntity,
                            TargetType = r.TargetType,
                            Confidence = r.Confidence,
                            ExtractedAt = DateTime.UtcNow
                        }).ToList();

                        _db.GameEntityRelations.AddRange(entities);
                        try
                        {
                            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                        }
                        catch (DbUpdateConcurrencyException ex)
                        {
                            MeepleAiMetrics.RecordPdfConcurrencyConflict(
                                nameof(PdfProcessingPipelineService),
                                MeepleAiMetrics.PdfConcurrencyCategories.B);
                            _logger.LogWarning(ex,
                                "Concurrency conflict on PdfDocument {PdfId} in {Handler} (Category B) — admin mutation wins, pipeline will re-read on next tick",
                                pdfId, nameof(PdfProcessingPipelineService));
                            DetachUnsavedChanges<GameEntityRelationEntity>();
                        }

                        _logger.LogInformation(
                            "[PdfPipeline] Graph RAG: extracted {RelCount} relations for PDF {PdfId}",
                            entities.Count, pdfDoc.Id);
                    }
                }
#pragma warning disable CA1031 // Graph RAG is optional enhancement
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "[PdfPipeline] Graph RAG extraction failed for PDF {PdfId}, continuing",
                        pdfDoc.Id);
                    DetachUnsavedChanges<GameEntityRelationEntity>();
                }
#pragma warning restore CA1031
            }

            // Issue #4215: Transition to Embedding state
            pdfDoc.ProcessingState = nameof(PdfProcessingState.Embedding);
            try
            {
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                MeepleAiMetrics.RecordPdfConcurrencyConflict(
                    nameof(PdfProcessingPipelineService),
                    MeepleAiMetrics.PdfConcurrencyCategories.B);
                _logger.LogWarning(ex,
                    "Concurrency conflict on PdfDocument {PdfId} in {Handler} (Category B) — admin mutation wins, pipeline will re-read on next tick",
                    pdfId, nameof(PdfProcessingPipelineService));
                return; // CRITICAL: do not throw — Quartz must see job as successful
            }

            // Step 4a: Generate embeddings (for all chunks: original + translated)
            var allChunkInputs = translatedChunks.Select(t => t.chunk).ToList();
            _logger.LogInformation("[PdfPipeline] Step 4a/5: Generating embeddings for {ChunkCount} chunks for {PdfId}", allChunkInputs.Count, pdfId);
            var embeddings = await GenerateEmbeddingsAsync(pdfDoc, allChunkInputs, cancellationToken).ConfigureAwait(false);

            // Issue #4215: Transition to Indexing state
            pdfDoc.ProcessingState = nameof(PdfProcessingState.Indexing);
            try
            {
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                MeepleAiMetrics.RecordPdfConcurrencyConflict(
                    nameof(PdfProcessingPipelineService),
                    MeepleAiMetrics.PdfConcurrencyCategories.B);
                _logger.LogWarning(ex,
                    "Concurrency conflict on PdfDocument {PdfId} in {Handler} (Category B) — admin mutation wins, pipeline will re-read on next tick",
                    pdfId, nameof(PdfProcessingPipelineService));
                return; // CRITICAL: do not throw — Quartz must see job as successful
            }

            // Step 4b: Index in pgvector.
            // Order matters: SaveTextChunksAsync MUST run BEFORE IndexInVectorStoreAsync.
            // IndexInVectorStoreAsync denormalizes role_tags + source_chunk_id onto the pgvector
            // rows by reading the freshly-saved (and freshly-classified) text_chunks keyed by
            // ChunkIndex. Both consume the same `translatedChunks`/`allChunkInputs` order, so the
            // join by ChunkIndex is stable. If indexing ran first the lookup would be empty and every
            // pgvector row would be born with role_tags=0 / source_chunk_id=null (silent corpus-wide
            // role-boost loss — see FusionSignals.ComputeRoleMatchBoost).
            _logger.LogInformation("[PdfPipeline] Step 4b/5: Indexing {ChunkCount} chunks for {PdfId}", allChunkInputs.Count, pdfId);
            await SaveTextChunksAsync(pdfDoc, allChunkInputs, cancellationToken).ConfigureAwait(false);
            await IndexInVectorStoreAsync(pdfDoc, translatedChunks, embeddings, cancellationToken).ConfigureAwait(false);

            // Issue #4215: Mark as Ready (final state)
            pdfDoc.ProcessingState = nameof(PdfProcessingState.Ready);
            pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
            // Issue #3269 (SP3): stamp the current indexer version so a completed fresh ingest is
            // the current pipeline version (v1.2 coordinate-aware after #3409/SP-E) and
            // `IndexerVersion == null` means only true pre-versioning legacy — keeps the bulk
            // re-index selector from redundantly re-processing fresh docs.
            // Null-coalescing (not overwrite): a reindex path already stamped its chosen version at
            // reset (ReindexDocumentCommandHandler), so we preserve that explicit choice here.
            pdfDoc.IndexerVersion ??= IndexerVersionRegistry.Current.Version;
            try
            {
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                MeepleAiMetrics.RecordPdfConcurrencyConflict(
                    nameof(PdfProcessingPipelineService),
                    MeepleAiMetrics.PdfConcurrencyCategories.B);
                _logger.LogWarning(ex,
                    "Concurrency conflict on PdfDocument {PdfId} in {Handler} (Category B) — admin mutation wins, pipeline will re-read on next tick",
                    pdfId, nameof(PdfProcessingPipelineService));
                return; // CRITICAL: do not throw — Quartz must see job as successful
            }

            _logger.LogInformation("[PdfPipeline] Successfully processed PDF {PdfId}: {Pages} pages, {Chunks} chunks (incl. translations)",
                pdfId, pdfDoc.PageCount ?? 0, translatedChunks.Count);
        }
#pragma warning disable CA1031 // Background pipeline must catch all to mark status
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "[PdfPipeline] Processing cancelled for PDF {PdfId}", pdfId);
            throw; // Let caller handle cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PdfPipeline] Processing FAILED for PDF {PdfId}", pdfId);
            await TryMarkFailedAsync(pdfDocumentId, ex.Message).ConfigureAwait(false);
        }
#pragma warning restore CA1031
    }

    private async Task<(string fullText, PagedTextExtractionResult result)> ExtractTextAsync(
        PdfDocumentEntity pdfDoc,
        string filePath,
        CancellationToken cancellationToken)
    {
        // Issue #501: Use blob storage with correct GUID format (no hyphens) to match StoreAsync key format
        // Task 4: bucket key decoupled from gameId — uses pdf.Id (see PdfStorageKey + rebucket scripts)
        // Issue #2671: StoreAsync writes the blob under a RANDOM fileId (persisted in FilePath), NOT pdfId.
        // Recover that fileId from FilePath; ForPdf(Id) is the resourceKey folder, not the fileId. The
        // ?? fallback preserves legacy behaviour for records with an empty/unparsable FilePath.
        var resourceKey = PdfStorageKey.ForPdf(pdfDoc.Id);
        var fileId = PdfStorageKey.FileIdFromPath(pdfDoc.FilePath) ?? resourceKey;
        var fileStream = await _blobStorageService.RetrieveAsync(fileId, BlobCategory.Pdf, resourceKey, cancellationToken).ConfigureAwait(false);

        if (fileStream == null)
        {
            // Fallback to local filesystem for backward compatibility (dev without S3)
            _logger.LogWarning("[PdfPipeline] Blob storage returned null for {PdfId}, falling back to filesystem path: {FilePath}", fileId, filePath);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(
                    $"PDF file not found in blob storage or filesystem: {filePath}", filePath);
            }
            fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        await using (fileStream.ConfigureAwait(false))
        {
            var extractResult = await _pdfTextExtractor
                .ExtractPagedTextAsync(fileStream, enableOcrFallback: true, cancellationToken)
                .ConfigureAwait(false);

            if (!extractResult.Success)
            {
                throw new InvalidOperationException(
                    $"Text extraction failed: {extractResult.ErrorMessage}");
            }

            var fullText = string.Join("\n\n", extractResult.PageChunks
                .Where(pc => !pc.IsEmpty)
                .Select(pc => pc.Text));

            pdfDoc.ExtractedText = fullText;
            pdfDoc.StructuredElementsJson = extractResult.StructuredElements is null
                ? null
                : JsonSerializer.Serialize(extractResult.StructuredElements);
            pdfDoc.PageCount = extractResult.TotalPages;
            pdfDoc.CharacterCount = extractResult.TotalCharacters;
            try
            {
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                MeepleAiMetrics.RecordPdfConcurrencyConflict(
                    nameof(PdfProcessingPipelineService),
                    MeepleAiMetrics.PdfConcurrencyCategories.B);
                _logger.LogWarning(ex,
                    "Concurrency conflict on PdfDocument {PdfId} in {Handler} (Category B) — admin mutation wins, pipeline will re-read on next tick",
                    pdfDoc.Id, nameof(PdfProcessingPipelineService));
                return (fullText, extractResult);
            }

            return (fullText, extractResult);
        }
    }

    /// <summary>
    /// Issue #1831 (L4) — render the first significant PDF page as a webp
    /// cover image and persist into R2 + entity columns. Best-effort: any
    /// failure is recorded on the entity (<c>CoverGenerationStatus=Failed</c>
    /// + <c>CoverGenerationError</c>) and the pipeline continues — the L1
    /// placeholder remains visible on the client.
    /// </summary>
    private async Task ExtractCoverImageAsync(
        PdfDocumentEntity pdfDoc,
        string filePath,
        CancellationToken cancellationToken)
    {
        if (_pdfCoverExtractor is null || _pdfCoverUploadPipeline is null)
        {
            // Cover services not registered (unit-test scenarios) — leave default Pending.
            return;
        }

        try
        {
            // Issue #1831 Gap B fix (audit 2026-06-02-cover-stack-live-audit.md):
            // ExtractTextAsync (linee 437-451) usa _blobStorageService.RetrieveAsync con
            // filesystem fallback — in prod il PDF vive in R2/S3, non su disk. Il pattern
            // originale (File.ReadAllBytesAsync) faceva fallire silenziosamente l'estrazione
            // cover su staging/prod (CoverGenerationStatus="Failed" permanente).
            var pdfBytes = await LoadPdfBytesAsync(pdfDoc.Id, filePath, cancellationToken).ConfigureAwait(false);
            var result = await _pdfCoverExtractor.ExtractAsync(pdfBytes, cancellationToken).ConfigureAwait(false);

            switch (result.Outcome)
            {
                case PdfCoverExtractionOutcome.Generated:
                    {
                        // Issue #2947: deterministic DB key; the pipeline writes the
                        // physical R2 object "{dbKey}-preview.webp" that the resolver
                        // reconstructs. Only the preview size is uploaded (the
                        // resolver never reads the thumbnail size).
                        // #3384 D5-A: DB key comes from the single CoverKeyBuilder.
                        var dbKey = CoverKeyBuilder.ForPdf(pdfDoc.Id).DbKey;

                        var persistedKey = await _pdfCoverUploadPipeline
                            .UploadAsync(dbKey, result.PreviewWebp!, cancellationToken)
                            .ConfigureAwait(false);

                        pdfDoc.CoverR2Key = persistedKey;
                        pdfDoc.CoverGenerationStatus = "Generated";
                        pdfDoc.CoverPageIndex = result.SelectedPageIndex;
                        pdfDoc.CoverGenerationError = null;
                        // #3373 D1: a successful generation closes the retry cycle — reset the budget
                        // so a later orphan-reset (Generated→Pending) starts fresh, not pre-exhausted.
                        pdfDoc.CoverGenerationAttempts = 0;
                        MeepleAiMetrics.RecordPdfCoverGeneration(MeepleAiMetrics.CoverGenerationOutcomeGenerated);

                        // Issue #1852 (Gap A): raise the propagation event so
                        // PdfCoverGeneratedEventHandler can populate SharedGame.PdfCoverR2Key.
                        _eventCollector?.Collect(new PdfCoverGeneratedEvent(
                            pdfDocumentId: pdfDoc.Id,
                            sharedGameId: pdfDoc.SharedGameId,
                            coverR2Key: persistedKey,
                            coverPageIndex: result.SelectedPageIndex ?? 0));

                        _logger.LogInformation(
                            "[PdfPipeline] Cover image generated for PDF {PdfId} from page {PageIndex} (dbKey={DbKey})",
                            pdfDoc.Id, result.SelectedPageIndex, persistedKey);
                        break;
                    }
                case PdfCoverExtractionOutcome.Skipped:
                    pdfDoc.CoverGenerationStatus = "Skipped";
                    pdfDoc.CoverPageIndex = result.SelectedPageIndex;
                    MeepleAiMetrics.RecordPdfCoverGeneration(MeepleAiMetrics.CoverGenerationOutcomeSkipped);
                    _logger.LogInformation(
                        "[PdfPipeline] Cover extraction skipped for PDF {PdfId} (heuristic rejected first 3 pages)",
                        pdfDoc.Id);
                    break;
                case PdfCoverExtractionOutcome.Failed:
                    pdfDoc.CoverGenerationStatus = "Failed";
                    pdfDoc.CoverGenerationError = result.ErrorMessage;
                    MeepleAiMetrics.RecordPdfCoverGeneration(MeepleAiMetrics.CoverGenerationOutcomeFailed);
                    _logger.LogWarning(
                        "[PdfPipeline] Cover extraction failed for PDF {PdfId}: {Error}",
                        pdfDoc.Id, result.ErrorMessage);
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // #3373 D1: an exception here is an infra failure (R2 upload / DB) — TRANSIENT.
            // Return to Pending (retry-eligible via BackfillPdfCoversJob) until
            // PdfCoverRetryPolicy.MaxAttempts, then terminal Failed.
            var (retryStatus, retryAttempts) = PdfCoverRetryPolicy.NextAfterTransientFailure(pdfDoc.CoverGenerationAttempts);
            pdfDoc.CoverGenerationStatus = retryStatus.ToString();
            pdfDoc.CoverGenerationAttempts = retryAttempts;
            pdfDoc.CoverGenerationError = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            // #3373 D1/D5-C: tag terminal vs still-retrying so the failed-ratio alert stays diagnostic.
            MeepleAiMetrics.RecordPdfCoverGeneration(retryAttempts >= PdfCoverRetryPolicy.MaxAttempts
                ? MeepleAiMetrics.CoverGenerationOutcomeFailed
                : MeepleAiMetrics.CoverGenerationOutcomeRetrying);
            _logger.LogWarning(ex,
                "[PdfPipeline] Cover extraction threw for PDF {PdfId} — continuing pipeline without cover",
                pdfDoc.Id);
        }
    }

    private async Task ExtractStructuredContentAsync(
        PdfDocumentEntity pdfDoc,
        string filePath,
        CancellationToken cancellationToken)
    {
        try
        {
            var structuredResult = await _tableExtractor
                .ExtractStructuredContentAsync(filePath, cancellationToken)
                .ConfigureAwait(false);

            if (!structuredResult.Success)
                return;

            pdfDoc.ExtractedTables = JsonSerializer.Serialize(structuredResult.Tables);
            pdfDoc.ExtractedDiagrams = JsonSerializer.Serialize(
                structuredResult.Diagrams.Select(d => new
                {
                    d.PageNumber,
                    d.DiagramType,
                    d.Description,
                    d.Width,
                    d.Height
                }));
            pdfDoc.AtomicRules = JsonSerializer.Serialize(structuredResult.AtomicRules);
            pdfDoc.TableCount = structuredResult.TableCount;
            pdfDoc.DiagramCount = structuredResult.DiagramCount;
            pdfDoc.AtomicRuleCount = structuredResult.AtomicRuleCount;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            MeepleAiMetrics.RecordPdfConcurrencyConflict(
                nameof(PdfProcessingPipelineService),
                MeepleAiMetrics.PdfConcurrencyCategories.B);
            _logger.LogWarning(ex,
                "Concurrency conflict on PdfDocument {PdfId} in {Handler} (Category B) — admin mutation wins, pipeline will re-read on next tick",
                pdfDoc.Id, nameof(PdfProcessingPipelineService));
        }
#pragma warning disable CA1031 // Structured extraction is optional, don't fail the pipeline
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PdfPipeline] Structured content extraction failed for PDF {PdfId}, continuing",
                pdfDoc.Id);
        }
#pragma warning restore CA1031
    }

    private async Task<List<DocumentChunkInput>> ChunkTextAsync(
        string fullText,
        PagedTextExtractionResult extractResult,
        Guid documentId,
        Guid? gameId,
        CancellationToken cancellationToken)
    {
        // Issue #3281: heading-aware production when AdvancedChunkingService is available.
        if (_advancedChunking != null)
        {
            var hierarchical = await HeadingAwareChunker.BuildAsync(
                extractResult.StructuredElements,
                fullText,
                documentId,
                gameId,
                _advancedChunking,
                cancellationToken).ConfigureAwait(false);
            return HeadingAwareChunkAdapter.ToChunkInputs(hierarchical);
        }

        // Fallback: flat production (pre-Slice-D behaviour) when the chunker is unavailable.
        var chunks = _chunkingService.PrepareForEmbedding(fullText, ChunkSize, ChunkOverlap)
            ?.Where(c => c != null && !string.IsNullOrWhiteSpace(c.Text))
            .ToList()
            ?? [];

        // Fallback: page-by-page chunking if whole-text chunking produced nothing
        if (chunks.Count == 0)
        {
            foreach (var pageChunk in extractResult.PageChunks.Where(pc => !pc.IsEmpty))
            {
                var pageTextChunks = _chunkingService.ChunkText(pageChunk.Text, ChunkSize, ChunkOverlap);

                foreach (var textChunk in pageTextChunks.Where(t => !string.IsNullOrWhiteSpace(t.Text)))
                {
                    chunks.Add(new DocumentChunkInput
                    {
                        Text = textChunk.Text,
                        Page = pageChunk.PageNumber,
                        CharStart = textChunk.CharStart,
                        CharEnd = textChunk.CharEnd
                    });
                }
            }
        }

        return chunks.Where(c => c != null && !string.IsNullOrWhiteSpace(c.Text)).ToList();
    }

    private async Task<List<float[]>> GenerateEmbeddingsAsync(
        PdfDocumentEntity pdfDoc,
        List<DocumentChunkInput> chunks,
        CancellationToken cancellationToken)
    {
        var allEmbeddings = new List<float[]>();
        var batchCount = (int)Math.Ceiling((double)chunks.Count / EmbeddingBatchSize);

        for (var batchIndex = 0; batchIndex < batchCount; batchIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var skip = batchIndex * EmbeddingBatchSize;
            var batchTexts = chunks.Skip(skip).Take(EmbeddingBatchSize)
                .Select(c => HeadingAwareChunkAdapter.CapForEmbedding(c.Text))
                .ToList();

            _logger.LogInformation("[PdfPipeline] Embedding batch {Current}/{Total} ({Count} texts)",
                batchIndex + 1, batchCount, batchTexts.Count);

            var batchResult = await _embeddingService
                .GenerateEmbeddingsAsync(batchTexts, cancellationToken)
                .ConfigureAwait(false);

            if (!batchResult.Success)
            {
                throw new InvalidOperationException(
                    $"Embedding batch {batchIndex + 1}/{batchCount} failed: {batchResult.ErrorMessage}");
            }

            if (batchResult.Embeddings == null || batchResult.Embeddings.Count != batchTexts.Count)
            {
                throw new InvalidOperationException(
                    $"Embedding batch {batchIndex + 1} returned {batchResult.Embeddings?.Count ?? 0} vectors for {batchTexts.Count} texts");
            }

            // Validate vectors
            foreach (var embedding in batchResult.Embeddings)
            {
                if (embedding == null || embedding.Length == 0
                    || Array.Exists(embedding, v => float.IsNaN(v) || float.IsInfinity(v)))
                {
                    throw new InvalidOperationException(
                        $"Invalid embedding vector detected in batch {batchIndex + 1}");
                }
            }

            allEmbeddings.AddRange(batchResult.Embeddings);
        }

        if (allEmbeddings.Count != chunks.Count)
        {
            throw new InvalidOperationException(
                $"Total embeddings {allEmbeddings.Count} != total chunks {chunks.Count}");
        }

        return allEmbeddings;
    }

    private async Task IndexInVectorStoreAsync(
        PdfDocumentEntity pdfDoc,
        List<(DocumentChunkInput chunk, string lang, bool isTranslation)> translatedChunks,
        List<float[]> embeddings,
        CancellationToken cancellationToken)
    {
        var chunkCount = translatedChunks.Count;

        // VectorDocument create/update + domain event publication now centralised
        // in IPdfIndexingPipeline (#2244 / epic #2242). The pipeline handles the
        // DbUpdateConcurrencyException internally with the same "Quartz must see
        // success" semantics this path needs.
        await _indexingPipeline.IndexAsync(
            pdfDocumentId: pdfDoc.Id,
            gameId: pdfDoc.SharedGameId,
            sharedGameId: pdfDoc.SharedGameId,
            chunkCount: chunkCount,
            totalCharacters: pdfDoc.ExtractedText?.Length ?? 0,
            language: string.IsNullOrWhiteSpace(pdfDoc.Language) ? "en" : pdfDoc.Language,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        // Re-read the (now persisted) entity so the pgvector block below has
        // its Id — the pipeline owns the SaveChanges, but the rest of this
        // method still needs the tracked row to delete stale embeddings.
        var vectorDoc = await _db.VectorDocuments
            .AsTracking()
            .FirstOrDefaultAsync(v => v.PdfDocumentId == pdfDoc.Id, cancellationToken)
            .ConfigureAwait(false);
        if (vectorDoc is null)
        {
            // Pipeline swallowed a DbUpdateConcurrencyException and the row was never
            // written — Quartz must still see job success, so we cannot rethrow. But
            // silently skipping pgvector indexing here means the PDF is "Ready" with
            // no embeddings until something forces a re-index. Surface the loss so
            // ops can detect it: increment the same Category-B counter the pipeline
            // itself uses, then log a WARN with the PDF id. The companion follow-up
            // (issue tracked in #2248 Sub #6) wires a dedicated Prometheus gauge
            // meepleai_pdf_indexed_no_kb_flag_total — this counter feeds into the
            // same dashboard.
            MeepleAiMetrics.RecordPdfConcurrencyConflict(
                nameof(PdfProcessingPipelineService),
                MeepleAiMetrics.PdfConcurrencyCategories.B);
            _logger.LogWarning(
                "PdfPipeline: VectorDocument row for PDF {PdfId} missing after IndexAsync — pipeline swallowed a concurrency conflict, pgvector embeddings skipped. PDF will need re-index.",
                pdfDoc.Id);
            return;
        }

        // Index embeddings in pgvector for semantic search
        if (_vectorStore != null && embeddings.Count == translatedChunks.Count)
        {
            // GameId resolution: same strategy as IndexPdfCommandHandler.effectiveGameId
            var gameId = pdfDoc.PrivateGameId ?? pdfDoc.SharedGameId ?? Guid.Empty;
            if (gameId == Guid.Empty)
            {
                _logger.LogWarning(
                    "[PdfPipeline] No GameId for PDF {PdfId}, skipping pgvector indexing",
                    pdfDoc.Id);
                return;
            }

            // Ensure pgvector table + HNSW index exist (idempotent)
            var dimension = embeddings[0].Length;
            await _vectorStore.EnsureCollectionExistsAsync(gameId, dimension, cancellationToken)
                .ConfigureAwait(false);

            // Delete old embeddings for this document (re-processing support)
            await _vectorStore.DeleteByVectorDocumentIdAsync(vectorDoc.Id, cancellationToken)
                .ConfigureAwait(false);

            // Build Embedding domain objects and bulk-insert via pgvector COPY.
            // Issue #1391: text_chunks rows were saved earlier in the pipeline with role_tags
            // populated by TextChunkRoleClassifier. We load them now (one query) to denormalize
            // role_tags + source_chunk_id into pgvector_embeddings so semantic-mode searches
            // can apply the role-match boost without joining the parent table.
            var textChunkLookup = await _db.TextChunks
                .Where(tc => tc.PdfDocumentId == pdfDoc.Id)
                .Select(tc => new { tc.Id, tc.ChunkIndex, tc.RoleTags })
                .ToDictionaryAsync(tc => tc.ChunkIndex, cancellationToken)
                .ConfigureAwait(false);

            // Tripwire: SaveTextChunksAsync must have run earlier in ProcessAsync so this lookup is
            // populated. If it is empty while we have chunks to index, the pgvector rows would be
            // denormalized with role_tags=0 / source_chunk_id=null (the ordering bug this method's
            // precondition guards against). Surface it rather than silently degrading RAG quality.
            if (textChunkLookup.Count == 0 && translatedChunks.Count > 0)
            {
                _logger.LogWarning(
                    "[PdfPipeline] text_chunks lookup empty for PDF {PdfId} while indexing {ChunkCount} chunks — role_tags/source_chunk_id will not be denormalized onto pgvector rows. Was SaveTextChunksAsync run before IndexInVectorStoreAsync?",
                    pdfDoc.Id, translatedChunks.Count);
            }

            var modelName = _embeddingService.GetModelName();
            var embeddingEntities = translatedChunks.Select((item, i) =>
            {
                textChunkLookup.TryGetValue(i, out var tc);
                return new KbEntities.Embedding(
                    id: Guid.NewGuid(),
                    vectorDocumentId: vectorDoc.Id,
                    textContent: item.chunk.Text,
                    vector: new KbValueObjects.Vector(embeddings[i]),
                    model: modelName,
                    chunkIndex: i,
                    pageNumber: Math.Max(1, item.chunk.Page),
                    language: item.lang,
                    sourceChunkId: tc?.Id,
                    isTranslation: item.isTranslation,
                    roleTags: (int)(tc?.RoleTags ?? GameBookRole.None));
            }).ToList();

            await _vectorStore.IndexBatchAsync(embeddingEntities, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "[PdfPipeline] Indexed {Count} embeddings in pgvector for PDF {PdfId} (gameId={GameId})",
                embeddingEntities.Count, pdfDoc.Id, gameId);
        }
    }

    private async Task SaveTextChunksAsync(
        PdfDocumentEntity pdfDoc,
        List<DocumentChunkInput> chunks,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Delete existing chunks for re-processing
        var existingChunks = await _db.TextChunks
            .Where(tc => tc.PdfDocumentId == pdfDoc.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (existingChunks.Count > 0)
        {
            _db.TextChunks.RemoveRange(existingChunks);
        }

        // text_chunks.GameId is FK to games.Id (NOT shared_games.id) — see PdfGameIdResolver.
        var resolvedGameId = await PdfGameIdResolver.ResolveAsync(_db, pdfDoc, cancellationToken)
            .ConfigureAwait(false);

        var textChunkEntities = chunks
            .Select((chunk, index) => new TextChunkEntity
            {
                Id = chunk.Id == Guid.Empty ? Guid.NewGuid() : chunk.Id,
                GameId = resolvedGameId,
                SharedGameId = pdfDoc.SharedGameId,
                PdfDocumentId = pdfDoc.Id,
                Content = chunk.Text,
                ChunkIndex = index,
                PageNumber = chunk.Page,
                CharacterCount = chunk.Text.Length,
                CreatedAt = now,
                // Issue #730: persist chunk hierarchy fields from chunking pipeline
                Heading = chunk.Heading,
                Level = chunk.Level,
                ParentChunkId = chunk.ParentChunkId,
                ElementType = chunk.ElementType,
                // SP-A (#3405): persist char offsets for citation grounding
                CharStart = chunk.CharStart,
                CharEnd = chunk.CharEnd,
                // SP-B (#3406): persist the normalized region for citation grounding
                BoundingBoxesJson = ChunkBoundingBoxJson.Serialize(chunk.BBox, chunk.Page)
            })
            .ToList();

        // Phase D4: classify chunks by GameBookRole (Tutorial/RulesReference/Narrative/etc.)
        // before persistence so the role_tags column is populated on insert.
        await TextChunkRoleClassifier.AssignRoleTagsAsync(
            _roleClassifier, textChunkEntities, chunks, _logger, cancellationToken)
            .ConfigureAwait(false);

        _db.TextChunks.AddRange(textChunkEntities);
        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            MeepleAiMetrics.RecordPdfConcurrencyConflict(
                nameof(PdfProcessingPipelineService),
                MeepleAiMetrics.PdfConcurrencyCategories.B);
            _logger.LogWarning(ex,
                "Concurrency conflict on PdfDocument {PdfId} in {Handler} (Category B) — admin mutation wins, pipeline will re-read on next tick",
                pdfDoc.Id, nameof(PdfProcessingPipelineService));
            return; // CRITICAL: do not throw — Quartz must see job as successful
        }

        _logger.LogInformation("[PdfPipeline] Saved {Count} text chunks for hybrid search (PDF {PdfId})",
            textChunkEntities.Count, pdfDoc.Id);
    }

    private async Task SaveRaptorSummariesAsync(
        Guid pdfDocumentId, Guid gameId,
        List<RaptorSummaryNode> summaries,
        CancellationToken ct)
    {
        foreach (var summary in summaries)
        {
            var entity = new RaptorSummaryEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = pdfDocumentId,
                GameId = gameId,
                TreeLevel = summary.TreeLevel,
                ClusterIndex = summary.ClusterIndex,
                SummaryText = summary.SummaryText,
                SourceChunkCount = summary.SourceChunkCount,
                CreatedAt = DateTime.UtcNow
            };
            _db.RaptorSummaries.Add(entity);
        }
        try
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            MeepleAiMetrics.RecordPdfConcurrencyConflict(
                nameof(PdfProcessingPipelineService),
                MeepleAiMetrics.PdfConcurrencyCategories.B);
            _logger.LogWarning(ex,
                "Concurrency conflict on RaptorSummaries for PDF {PdfId} in {Handler} (Category B) — admin mutation wins, pipeline will re-read on next tick",
                pdfDocumentId, nameof(PdfProcessingPipelineService));
        }
    }

    /// <summary>
    /// Detaches Added/Modified entities of a given type from the change tracker.
    /// Used in catch blocks of optional, non-blocking enhancement steps so a
    /// failed SaveChangesAsync does not poison subsequent saves with the same
    /// unflushed entities (and thus the same error).
    /// </summary>
    private void DetachUnsavedChanges<TEntity>() where TEntity : class
    {
        var entries = _db.ChangeTracker.Entries<TEntity>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified)
            .ToList();
        foreach (var entry in entries)
        {
            entry.State = EntityState.Detached;
        }
    }

    /// <summary>
    /// Issue #1831 Gap B (audit 2026-06-02): load PDF bytes con priorità blob storage
    /// (R2/S3 in prod) → filesystem fallback (dev senza bucket). Stesso pattern usato da
    /// <see cref="ExtractTextAsync"/> ma materializzato in byte[] perché
    /// <see cref="IPdfCoverExtractor.ExtractAsync"/> richiede un byte array (Docnet API).
    /// </summary>
    private async Task<byte[]> LoadPdfBytesAsync(
        Guid pdfDocumentId,
        string filePath,
        CancellationToken cancellationToken)
    {
        // Issue #2671: the blob lives under a random fileId embedded in FilePath, not pdfId.
        // Recover it from the persisted path; ForPdf(Id) is the resourceKey folder. The ??
        // fallback preserves legacy behaviour for records with an empty/unparsable FilePath.
        var resourceKey = PdfStorageKey.ForPdf(pdfDocumentId);
        var fileId = PdfStorageKey.FileIdFromPath(filePath) ?? resourceKey;
        var stream = await _blobStorageService
            .RetrieveAsync(fileId, BlobCategory.Pdf, resourceKey, cancellationToken)
            .ConfigureAwait(false);

        if (stream is null)
        {
            // Fallback al filesystem locale per dev senza bucket (parity con ExtractTextAsync:444-451).
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(
                    $"PDF file not found in blob storage or filesystem: {filePath}", filePath);
            }
            stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        await using (stream.ConfigureAwait(false))
        {
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
            return memoryStream.ToArray();
        }
    }

    private async Task MarkFailedAsync(PdfDocumentEntity pdfDoc, string errorMessage)
    {
        // Issue #4215: Use Failed state
        pdfDoc.ProcessingState = nameof(PdfProcessingState.Failed);
        pdfDoc.ProcessingError = errorMessage;
        pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
        try
        {
            await _db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            MeepleAiMetrics.RecordPdfConcurrencyConflict(
                nameof(PdfProcessingPipelineService),
                MeepleAiMetrics.PdfConcurrencyCategories.B);
            _logger.LogWarning(ex,
                "Concurrency conflict on PdfDocument {PdfId} in {Handler} (Category B) — admin mutation wins, pipeline will re-read on next tick",
                pdfDoc.Id, nameof(PdfProcessingPipelineService));
            // CRITICAL: do not throw — Quartz must see job as successful
        }
    }

    /// <summary>
    /// Best-effort attempt to mark a PDF as failed. Used in catch blocks where
    /// the original DbContext may be in a bad state.
    /// </summary>
    private async Task TryMarkFailedAsync(Guid pdfDocumentId, string errorMessage)
    {
        try
        {
            var pdfDoc = await _db.PdfDocuments
                .FindAsync(new object[] { pdfDocumentId }, CancellationToken.None)
                .ConfigureAwait(false);

            if (pdfDoc != null
                && !string.Equals(pdfDoc.ProcessingState, nameof(PdfProcessingState.Ready), StringComparison.Ordinal))
            {
                // Issue #4215: Use Failed state
                pdfDoc.ProcessingState = nameof(PdfProcessingState.Failed);
                pdfDoc.ProcessingError = errorMessage.Length > 500
                    ? errorMessage[..500]
                    : errorMessage;
                pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
                await _db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (DbUpdateConcurrencyException ex)
        {
            MeepleAiMetrics.RecordPdfConcurrencyConflict(
                nameof(PdfProcessingPipelineService),
                MeepleAiMetrics.PdfConcurrencyCategories.B);
            _logger.LogWarning(ex,
                "Concurrency conflict on PdfDocument {PdfId} in {Handler} (Category B) — admin mutation wins, pipeline will re-read on next tick",
                pdfDocumentId, nameof(PdfProcessingPipelineService));
        }
#pragma warning disable CA1031 // Best-effort error marking must not throw
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PdfPipeline] Failed to mark PDF {PdfId} as failed", pdfDocumentId);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Issue #2947 test seam: exposes <see cref="ExtractCoverImageAsync"/> to the
    /// unit-test assembly (InternalsVisibleTo Api.Tests) so the deterministic
    /// cover-key behaviour can be asserted directly without driving the full
    /// ProcessAsync pipeline.
    /// </summary>
    internal Task InvokeExtractCoverImageForTestAsync(
        PdfDocumentEntity pdfDoc, string filePath, CancellationToken cancellationToken)
        => ExtractCoverImageAsync(pdfDoc, filePath, cancellationToken);
}

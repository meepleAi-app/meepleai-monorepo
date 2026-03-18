using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Services;
using Api.Services.Pdf;
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
    private const int ChunkSize = 512;
    private const int ChunkOverlap = 50;
    private const int EmbeddingBatchSize = 20;

    private readonly MeepleAiDbContext _db;
    private readonly IPdfTextExtractor _pdfTextExtractor;
    private readonly IPdfTableExtractor _tableExtractor;
    private readonly ITextChunkingService _chunkingService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IBlobStorageService _blobStorageService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PdfProcessingPipelineService> _logger;
    private readonly IRaptorIndexer? _raptorIndexer;
    private readonly IEntityExtractor? _entityExtractor;
    private readonly IQdrantVectorStoreAdapter? _vectorStore;

    public PdfProcessingPipelineService(
        MeepleAiDbContext db,
        IPdfTextExtractor pdfTextExtractor,
        IPdfTableExtractor tableExtractor,
        ITextChunkingService chunkingService,
        IEmbeddingService embeddingService,
        IBlobStorageService blobStorageService,
        TimeProvider timeProvider,
        ILogger<PdfProcessingPipelineService> logger,
        IRaptorIndexer? raptorIndexer = null,
        IEntityExtractor? entityExtractor = null,
        IQdrantVectorStoreAdapter? vectorStore = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _pdfTextExtractor = pdfTextExtractor ?? throw new ArgumentNullException(nameof(pdfTextExtractor));
        _tableExtractor = tableExtractor ?? throw new ArgumentNullException(nameof(tableExtractor));
        _chunkingService = chunkingService ?? throw new ArgumentNullException(nameof(chunkingService));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _blobStorageService = blobStorageService ?? throw new ArgumentNullException(nameof(blobStorageService));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _raptorIndexer = raptorIndexer;
        _entityExtractor = entityExtractor;
        _vectorStore = vectorStore;
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
            // Load and validate
            var pdfDoc = await _db.PdfDocuments
                .FindAsync(new object[] { pdfDocumentId }, cancellationToken)
                .ConfigureAwait(false);

            if (pdfDoc == null)
            {
                _logger.LogError("[PdfPipeline] PDF document {PdfId} not found in database", pdfId);
                return;
            }

            // Idempotency: skip if already completed or failed
            var readyState = nameof(PdfProcessingState.Ready);
            var failedState = nameof(PdfProcessingState.Failed);
            if (string.Equals(pdfDoc.ProcessingState, readyState, StringComparison.Ordinal)
                || string.Equals(pdfDoc.ProcessingState, failedState, StringComparison.Ordinal))
            {
                _logger.LogInformation(
                    "[PdfPipeline] PDF {PdfId} already in terminal state ({Status}), skipping",
                    pdfId, pdfDoc.ProcessingState);
                return;
            }

            // Issue #4215: Transition to Extracting state
            pdfDoc.ProcessingState = "Extracting";
            pdfDoc.ProcessingError = null;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Step 1: Extract text
            _logger.LogInformation("[PdfPipeline] Step 1/4: Extracting text from {PdfId}", pdfId);
            var (fullText, extractResult) = await ExtractTextAsync(pdfDoc, filePath, cancellationToken).ConfigureAwait(false);

            // Step 2: Extract structured content (tables)
            _logger.LogInformation("[PdfPipeline] Step 2/4: Extracting structured content from {PdfId}", pdfId);
            await ExtractStructuredContentAsync(pdfDoc, filePath, cancellationToken).ConfigureAwait(false);

            // Issue #4215: Transition to Chunking state
            pdfDoc.ProcessingState = "Chunking";
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Step 3: Chunk text
            _logger.LogInformation("[PdfPipeline] Step 3/4: Chunking text for {PdfId} ({CharCount} chars)", pdfId, fullText.Length);
            var chunks = ChunkText(fullText, extractResult);

            if (chunks.Count == 0)
            {
                _logger.LogWarning("[PdfPipeline] No chunks produced for {PdfId}, marking as failed", pdfId);
                await MarkFailedAsync(pdfDoc, "Text extraction produced no usable chunks").ConfigureAwait(false);
                return;
            }

            // === RAPTOR: Build hierarchical summary tree (optional, non-blocking) ===
            if (_raptorIndexer != null && chunks.Count > 3)
            {
                try
                {
                    var chunkTexts = chunks.Select(c => c.Text).ToList();
                    var gameId = pdfDoc.GameId ?? Guid.Empty;
                    var raptorResult = await _raptorIndexer.BuildTreeAsync(
                        pdfDoc.Id, gameId,
                        chunkTexts, maxLevels: 3, cancellationToken).ConfigureAwait(false);

                    if (raptorResult.TotalNodes > 0)
                    {
                        await SaveRaptorSummariesAsync(
                            pdfDoc.Id, gameId,
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
                    // Non-blocking: document processing continues even if RAPTOR fails
                }
#pragma warning restore CA1031
            }

            // === Graph RAG: Extract entity relations (optional, non-blocking) ===
            if (_entityExtractor is not null && fullText.Length >= 200)
            {
                try
                {
                    var gameId = pdfDoc.GameId ?? Guid.Empty;
                    var gameTitle = pdfDoc.FileName ?? "Unknown";
                    var extraction = await _entityExtractor.ExtractEntitiesAsync(
                        gameId, gameTitle,
                        fullText[..Math.Min(fullText.Length, 8000)],
                        cancellationToken).ConfigureAwait(false);

                    if (extraction.Relations.Count > 0)
                    {
                        var entities = extraction.Relations.Select(r => new GameEntityRelationEntity
                        {
                            Id = Guid.NewGuid(),
                            GameId = gameId,
                            SourceEntity = r.SourceEntity,
                            SourceType = r.SourceType,
                            Relation = r.Relation,
                            TargetEntity = r.TargetEntity,
                            TargetType = r.TargetType,
                            Confidence = r.Confidence,
                            ExtractedAt = DateTime.UtcNow
                        }).ToList();

                        _db.GameEntityRelations.AddRange(entities);
                        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

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
                }
#pragma warning restore CA1031
            }

            // Issue #4215: Transition to Embedding state
            pdfDoc.ProcessingState = "Embedding";
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Step 4a: Generate embeddings
            _logger.LogInformation("[PdfPipeline] Step 4a/5: Generating embeddings for {ChunkCount} chunks for {PdfId}", chunks.Count, pdfId);
            var embeddings = await GenerateEmbeddingsAsync(pdfDoc, chunks, cancellationToken).ConfigureAwait(false);

            // Issue #4215: Transition to Indexing state
            pdfDoc.ProcessingState = "Indexing";
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Step 4b: Index in Qdrant
            _logger.LogInformation("[PdfPipeline] Step 4b/5: Indexing {ChunkCount} chunks for {PdfId}", chunks.Count, pdfId);
            await IndexInQdrantAsync(pdfDoc, chunks, embeddings, cancellationToken).ConfigureAwait(false);
            await SaveTextChunksAsync(pdfDoc, chunks, cancellationToken).ConfigureAwait(false);

            // Issue #4215: Mark as Ready (final state)
            pdfDoc.ProcessingState = "Ready";
            pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("[PdfPipeline] Successfully processed PDF {PdfId}: {Pages} pages, {Chunks} chunks",
                pdfId, pdfDoc.PageCount ?? 0, chunks.Count);
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
        var fileId = pdfDoc.Id.ToString("N"); // "N" = no hyphens, matches S3BlobStorageService.StoreAsync
        var gameId = (pdfDoc.PrivateGameId ?? pdfDoc.GameId)?.ToString() ?? string.Empty;
        var fileStream = await _blobStorageService.RetrieveAsync(fileId, gameId, cancellationToken).ConfigureAwait(false);

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
            pdfDoc.PageCount = extractResult.TotalPages;
            pdfDoc.CharacterCount = extractResult.TotalCharacters;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return (fullText, extractResult);
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
#pragma warning disable CA1031 // Structured extraction is optional, don't fail the pipeline
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PdfPipeline] Structured content extraction failed for PDF {PdfId}, continuing",
                pdfDoc.Id);
        }
#pragma warning restore CA1031
    }

    private List<DocumentChunkInput> ChunkText(string fullText, PagedTextExtractionResult extractResult)
    {
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
            var batchTexts = chunks.Skip(skip).Take(EmbeddingBatchSize).Select(c => c.Text).ToList();

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

    private async Task IndexInQdrantAsync(
        PdfDocumentEntity pdfDoc,
        List<DocumentChunkInput> chunks,
        List<float[]> embeddings,
        CancellationToken cancellationToken)
    {
        var chunkCount = chunks.Count;

        // Update or create VectorDocument record (tracking)
        var vectorDoc = await _db.VectorDocuments
            .FirstOrDefaultAsync(v => v.PdfDocumentId == pdfDoc.Id, cancellationToken)
            .ConfigureAwait(false);

        if (vectorDoc == null)
        {
            vectorDoc = new VectorDocumentEntity
            {
                Id = Guid.NewGuid(),
                GameId = pdfDoc.GameId,
                SharedGameId = pdfDoc.SharedGameId,
                PdfDocumentId = pdfDoc.Id,
                IndexingStatus = "completed",
                ChunkCount = chunkCount,
                TotalCharacters = pdfDoc.ExtractedText?.Length ?? 0,
                IndexedAt = _timeProvider.GetUtcNow().UtcDateTime
            };
            _db.VectorDocuments.Add(vectorDoc);
        }
        else
        {
            vectorDoc.IndexingStatus = "completed";
            vectorDoc.ChunkCount = chunkCount;
            vectorDoc.TotalCharacters = pdfDoc.ExtractedText?.Length ?? 0;
            vectorDoc.IndexedAt = _timeProvider.GetUtcNow().UtcDateTime;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Index embeddings in pgvector for semantic search
        if (_vectorStore != null && embeddings.Count == chunks.Count)
        {
            var gameId = pdfDoc.GameId ?? pdfDoc.SharedGameId ?? Guid.Empty;
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

            // Build Embedding domain objects and bulk-insert via pgvector COPY
            var modelName = _embeddingService.GetModelName();
            var embeddingEntities = chunks.Select((chunk, i) =>
                new KbEntities.Embedding(
                    id: Guid.NewGuid(),
                    vectorDocumentId: vectorDoc.Id,
                    textContent: chunk.Text,
                    vector: new KbValueObjects.Vector(embeddings[i]),
                    model: modelName,
                    chunkIndex: i,
                    pageNumber: Math.Max(1, chunk.Page)))
                .ToList();

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

        var textChunkEntities = chunks
            .Select((chunk, index) => new TextChunkEntity
            {
                Id = Guid.NewGuid(),
                GameId = pdfDoc.GameId,
                PdfDocumentId = pdfDoc.Id,
                Content = chunk.Text,
                ChunkIndex = index,
                PageNumber = chunk.Page,
                CharacterCount = chunk.Text.Length,
                CreatedAt = now
            })
            .ToList();

        _db.TextChunks.AddRange(textChunkEntities);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

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
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private async Task MarkFailedAsync(PdfDocumentEntity pdfDoc, string errorMessage)
    {
        // Issue #4215: Use Failed state
        pdfDoc.ProcessingState = "Failed";
        pdfDoc.ProcessingError = errorMessage;
        pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
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
                && !string.Equals(pdfDoc.ProcessingState, "Ready", StringComparison.Ordinal))
            {
                // Issue #4215: Use Failed state
                pdfDoc.ProcessingState = "Failed";
                pdfDoc.ProcessingError = errorMessage.Length > 500
                    ? errorMessage[..500]
                    : errorMessage;
                pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
                await _db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
#pragma warning disable CA1031 // Best-effort error marking must not throw
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PdfPipeline] Failed to mark PDF {PdfId} as failed", pdfDocumentId);
        }
#pragma warning restore CA1031
    }
}

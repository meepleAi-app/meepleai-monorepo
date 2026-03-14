using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.Configuration;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PdfIndexingErrorCode = Api.BoundedContexts.DocumentProcessing.Application.DTOs.PdfIndexingErrorCode;

namespace Api.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>
/// Handler for IndexPdfCommand. Orchestrates the PDF indexing workflow:
/// 1. Load PDF document from repository
/// 2. Validate extraction status
/// 3. Chunk extracted text
/// 4. Generate embeddings
/// 5. Index to Qdrant
/// 6. Update PDF document status
/// </summary>
internal class IndexPdfCommandHandler : ICommandHandler<IndexPdfCommand, IndexingResultDto>
{
    private readonly MeepleAiDbContext _db;
    private readonly ITextChunkingService _chunkingService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IndexingSettings _indexingSettings;
    private readonly ILogger<IndexPdfCommandHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public IndexPdfCommandHandler(
        MeepleAiDbContext db,
        ITextChunkingService chunkingService,
        IEmbeddingService embeddingService,
        ILogger<IndexPdfCommandHandler> logger,
        IOptions<IndexingSettings> indexingSettings,
        TimeProvider? timeProvider = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _chunkingService = chunkingService ?? throw new ArgumentNullException(nameof(chunkingService));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _indexingSettings = indexingSettings?.Value ?? throw new ArgumentNullException(nameof(indexingSettings));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<IndexingResultDto> Handle(IndexPdfCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var pdfId = command.PdfId;
        _logger.LogInformation("Starting indexing for PDF {PdfId}", pdfId);

        try
        {
            // Step 1: Validate PDF and prepare for indexing
            var (validationSuccess, pdf, vectorDoc, validationError, errorCode) = await ValidateAndPreparePdfForIndexingAsync(
                pdfId, cancellationToken).ConfigureAwait(false);
            if (!validationSuccess)
            {
                return IndexingResultDto.CreateFailure(validationError!, errorCode!.Value);
            }

            // Track processing state: mark as Indexing (covers chunk + embed + index phases)
            pdf!.ProcessingState = "Indexing";
            pdf.IndexingStartedAt = _timeProvider.GetUtcNow().UtcDateTime;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Step 2: Chunk text and generate embeddings
            var (chunkingSuccess, documentChunks, chunkingError, chunkErrorCode) = await ChunkAndEmbedTextAsync(
                pdfId, pdf.ExtractedText!, cancellationToken).ConfigureAwait(false);
            if (!chunkingSuccess)
            {
                pdf.ProcessingState = "Failed";
                return await MarkIndexingFailedAsync(vectorDoc!, chunkingError!, chunkErrorCode!.Value, cancellationToken).ConfigureAwait(false);
            }

            // Step 3: Index in Qdrant and update VectorDocument
            // For private PDFs GameId is null — fall back to PrivateGameId so vectors are scoped
            // to the correct private game rather than collapsed under Guid.Empty.
            var effectiveGameId = pdf.GameId ?? pdf.PrivateGameId ?? Guid.Empty;
            var indexingSuccess = await IndexChunksInVectorStoreAsync(
                pdfId, effectiveGameId.ToString(), pdf.ExtractedText!, documentChunks!, vectorDoc!, cancellationToken).ConfigureAwait(false);
            if (!indexingSuccess)
            {
                pdf.ProcessingState = "Failed";
                return await MarkIndexingFailedAsync(vectorDoc!, "Qdrant indexing failed", PdfIndexingErrorCode.QdrantIndexingFailed, cancellationToken).ConfigureAwait(false);
            }

            // Step 4: Save text chunks to PostgreSQL for hybrid search
            await SaveTextChunksToPostgresAsync(pdfId, effectiveGameId, documentChunks!, cancellationToken).ConfigureAwait(false);

            // Mark processing complete
            pdf.ProcessingState = "Ready";
            pdf.ProcessingStatus = "completed";
            pdf.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Successfully indexed PDF {PdfId}: {ChunkCount} chunks, {TotalChars} characters",
                pdfId, documentChunks!.Count, pdf.ExtractedText!.Length);

            return IndexingResultDto.CreateSuccess(
                vectorDoc!.Id.ToString(),
                documentChunks.Count,
                vectorDoc.IndexedAt!.Value);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // SERVICE BOUNDARY PATTERN: Error state management for complex multi-system operation
        // PDF indexing involves multiple external systems (Qdrant, DB, file system) that must maintain consistency
#pragma warning restore S125
        catch (Exception ex)
        {
            // ERROR STATE MANAGEMENT: Top-level catch ensures graceful failure handling
            // Rationale: PDF indexing involves multiple external systems (Qdrant, DB, file system).
            // Any unhandled error should be captured, logged, and persisted as a failed indexing
            // attempt rather than throwing to the caller. This maintains data consistency and
            // provides operators with debugging context via the indexing_error field.
            // Context: Covers unforeseen errors after specific exception handlers above
            _logger.LogError(ex, "Unexpected error indexing PDF {PdfId}", pdfId);

            // Persist failed state so the PDF doesn't remain stuck in "Indexing"
            try
            {
                var failedPdf = await _db.PdfDocuments
                    .AsTracking()
                    .FirstOrDefaultAsync(p => p.Id.ToString() == pdfId, CancellationToken.None).ConfigureAwait(false);
                if (failedPdf != null)
                {
                    failedPdf.ProcessingState = "Failed";
                    failedPdf.ProcessingStatus = "failed";
                    failedPdf.ProcessingError = $"Unexpected error: {ex.Message}";
                    await _db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (Exception dbEx)
            {
                _logger.LogError(dbEx, "Failed to persist error state for PDF {PdfId}", pdfId);
            }

            return IndexingResultDto.CreateFailure($"Unexpected error: {ex.Message}", PdfIndexingErrorCode.UnexpectedError);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Validates PDF document and prepares VectorDocument for indexing with idempotency check.
    /// Returns (success, pdf, vectorDoc, errorMessage, errorCode).
    /// </summary>
    private async Task<(bool success, PdfDocumentEntity? pdf, VectorDocumentEntity? vectorDoc, string? errorMessage, PdfIndexingErrorCode? errorCode)> ValidateAndPreparePdfForIndexingAsync(
        string pdfId,
        CancellationToken cancellationToken)
    {
        // Retrieve PDF document with tracking enabled (global NoTracking default must be overridden)
        var pdf = await _db.PdfDocuments
            .AsTracking()
            .Include(p => p.Game)
            .FirstOrDefaultAsync(p => p.Id.ToString() == pdfId, cancellationToken).ConfigureAwait(false);

        if (pdf == null)
        {
            _logger.LogWarning("PDF {PdfId} not found", pdfId);
            return (false, null, null, "PDF not found", PdfIndexingErrorCode.PdfNotFound);
        }

        // Validate text extraction is complete
        if (string.IsNullOrWhiteSpace(pdf.ExtractedText))
        {
            _logger.LogWarning("PDF {PdfId} has no extracted text", pdfId);
            return (false, pdf, null, "PDF text extraction required. Please extract text before indexing.", PdfIndexingErrorCode.TextExtractionRequired);
        }

        // Check if already indexed (for idempotency)
        var pdfGuid = Guid.Parse(pdfId);
        var existingVectorDoc = await _db.Set<VectorDocumentEntity>()
            .FirstOrDefaultAsync(v => v.PdfDocumentId == pdfGuid, cancellationToken).ConfigureAwait(false);

        if (existingVectorDoc != null)
        {
            _logger.LogInformation("PDF {PdfId} already indexed, re-indexing", pdfId);

            // Update existing entity status to "processing"
            existingVectorDoc.IndexingStatus = "processing";
            existingVectorDoc.IndexingError = null;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Create new VectorDocumentEntity
            var embeddingDimensions = _embeddingService.GetEmbeddingDimensions();

            existingVectorDoc = new VectorDocumentEntity
            {
                Id = Guid.NewGuid(),
                GameId = pdf.GameId,
                SharedGameId = pdf.SharedGameId, // Issue #5185: propagate SharedGameId from PDF
                PdfDocumentId = pdfGuid,
                IndexingStatus = "processing",
                EmbeddingModel = _embeddingService.GetModelName(),
                EmbeddingDimensions = embeddingDimensions
            };
            _db.Set<VectorDocumentEntity>().Add(existingVectorDoc);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return (true, pdf, existingVectorDoc, null, null);
    }

    /// <summary>
    /// Chunks PDF text and generates embeddings.
    /// Returns (success, documentChunks, errorMessage, errorCode).
    /// </summary>
    private async Task<(bool success, List<DocumentChunk>? documentChunks, string? errorMessage, PdfIndexingErrorCode? errorCode)> ChunkAndEmbedTextAsync(
        string pdfId,
        string extractedText,
                CancellationToken cancellationToken)
    {
        // Chunk the text
        _logger.LogInformation("Chunking text for PDF {PdfId} ({CharCount} characters)",
            pdfId, extractedText.Length);

        var textChunks = _chunkingService.ChunkText(extractedText);

        if (textChunks.Count == 0)
        {
            _logger.LogWarning("No chunks created for PDF {PdfId}", pdfId);
            return (false, null, "No chunks created from text", PdfIndexingErrorCode.ChunkingFailed);
        }

        _logger.LogInformation("Created {ChunkCount} chunks for PDF {PdfId}", textChunks.Count, pdfId);

        // Generate embeddings in batches to reduce memory footprint
        var embeddingBatchSize = _indexingSettings.EmbeddingBatchSize;
        var documentChunks = new List<DocumentChunk>(textChunks.Count);

        _logger.LogInformation("Generating embeddings for {ChunkCount} chunks in batches of {BatchSize}",
            textChunks.Count, embeddingBatchSize);

        for (int i = 0; i < textChunks.Count; i += embeddingBatchSize)
        {
            var batchSize = Math.Min(embeddingBatchSize, textChunks.Count - i);

            _logger.LogDebug("Processing embedding batch {BatchNumber}/{TotalBatches} ({BatchSize} chunks)",
                (i / embeddingBatchSize) + 1,
                (int)Math.Ceiling((double)textChunks.Count / embeddingBatchSize),
                batchSize);

            var texts = textChunks.Skip(i).Take(batchSize).Select(c => c.Text).ToList();
            var embeddingResult = await _embeddingService.GenerateEmbeddingsAsync(texts, cancellationToken).ConfigureAwait(false);

            if (!embeddingResult.Success || embeddingResult.Embeddings.Count == 0)
            {
                _logger.LogError("Failed to generate embeddings for PDF {PdfId}: {Error}",
                    pdfId, embeddingResult.ErrorMessage);
                return (false, null, $"Embedding generation failed: {embeddingResult.ErrorMessage}", PdfIndexingErrorCode.EmbeddingFailed);
            }

            if (embeddingResult.Embeddings.Count != batchSize)
            {
                _logger.LogError("Embedding count mismatch: expected {Expected}, got {Actual}",
                    batchSize, embeddingResult.Embeddings.Count);
                return (false, null, "Embedding count mismatch", PdfIndexingErrorCode.EmbeddingFailed);
            }

            // Prepare document chunks with embeddings
            var batchChunks = textChunks.Skip(i).Take(batchSize)
                .Select((chunk, index) => new DocumentChunk
                {
                    Text = chunk.Text,
                    Embedding = embeddingResult.Embeddings[index],
                    Page = chunk.Page,
                    CharStart = chunk.CharStart,
                    CharEnd = chunk.CharEnd
                })
                .ToList();

            documentChunks.AddRange(batchChunks);

            _logger.LogDebug("Completed batch {BatchNumber}, total chunks processed: {ProcessedCount}/{TotalCount}",
                (i / embeddingBatchSize) + 1, documentChunks.Count, textChunks.Count);
        }

        return (true, documentChunks, null, null);
    }

    /// <summary>
    /// Indexes document chunks in Qdrant and updates VectorDocument.
    /// </summary>
    private Task<bool> IndexChunksInVectorStoreAsync(
        string pdfId,
        string gameId,
        string extractedText,
        List<DocumentChunk> documentChunks,
        VectorDocumentEntity vectorDoc,
        CancellationToken cancellationToken)
    {
        // Vector store (Qdrant) has been removed — skip vector indexing.
        // Update VectorDocumentEntity to "completed" for tracking.
        _logger.LogInformation("Skipping Qdrant indexing (removed) for PDF {PdfId}, {ChunkCount} chunks",
            pdfId, documentChunks.Count);

        vectorDoc.IndexingStatus = "completed";
        vectorDoc.ChunkCount = documentChunks.Count;
        vectorDoc.TotalCharacters = extractedText.Length;
        vectorDoc.IndexedAt = _timeProvider.GetUtcNow().UtcDateTime;
        vectorDoc.IndexingError = null;

        return Task.FromResult(true);
    }

    /// <summary>
    /// Saves text chunks to PostgreSQL for hybrid search with FTS.
    /// </summary>
    private async Task SaveTextChunksToPostgresAsync(
        string pdfId,
        Guid gameId,
        List<DocumentChunk> documentChunks,
        CancellationToken cancellationToken)
    {
        var pdfGuid = Guid.Parse(pdfId);

        // Delete existing chunks
        var existingChunks = await _db.TextChunks
            .Where(tc => tc.PdfDocumentId == pdfGuid)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        if (existingChunks.Count > 0)
        {
            _db.TextChunks.RemoveRange(existingChunks);
        }

        // Create new text chunk entities
        var textChunkEntities = documentChunks
            .Select((chunk, index) => new TextChunkEntity
            {
                Id = Guid.NewGuid(),
                GameId = gameId,
                PdfDocumentId = pdfGuid,
                Content = chunk.Text,
                ChunkIndex = index,
                PageNumber = chunk.Page,
                CharacterCount = chunk.Text.Length,
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
            })
            .ToList();

        _db.TextChunks.AddRange(textChunkEntities);
        _logger.LogInformation("Saved {ChunkCount} text chunks to PostgreSQL for hybrid search (PDF {PdfId})",
            textChunkEntities.Count, pdfId);
    }

    private async Task<IndexingResultDto> MarkIndexingFailedAsync(
        VectorDocumentEntity vectorDoc,
        string errorMessage,
        PdfIndexingErrorCode errorCode,
        CancellationToken cancellationToken)
    {
        vectorDoc.IndexingStatus = "failed";
        vectorDoc.IndexingError = errorMessage;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return IndexingResultDto.CreateFailure(errorMessage, errorCode);
    }
}
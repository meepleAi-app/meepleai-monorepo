using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    private readonly IQdrantService _qdrantService;
    private readonly ILogger<IndexPdfCommandHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public IndexPdfCommandHandler(
        MeepleAiDbContext db,
        ITextChunkingService chunkingService,
        IEmbeddingService embeddingService,
        IQdrantService qdrantService,
        ILogger<IndexPdfCommandHandler> logger,
        TimeProvider? timeProvider = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _chunkingService = chunkingService ?? throw new ArgumentNullException(nameof(chunkingService));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _qdrantService = qdrantService ?? throw new ArgumentNullException(nameof(qdrantService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

            // Step 2: Chunk text and generate embeddings
            var (chunkingSuccess, documentChunks, chunkingError, chunkErrorCode) = await ChunkAndEmbedTextAsync(
                pdfId, pdf!.ExtractedText!, cancellationToken).ConfigureAwait(false);
            if (!chunkingSuccess)
            {
                return await MarkIndexingFailedAsync(vectorDoc!, chunkingError!, chunkErrorCode!.Value, cancellationToken).ConfigureAwait(false);
            }

            // Step 3: Index in Qdrant and update VectorDocument
            var indexingSuccess = await IndexChunksInVectorStoreAsync(
                pdfId, pdf.GameId.ToString(), pdf.ExtractedText!, documentChunks!, vectorDoc!, cancellationToken).ConfigureAwait(false);
            if (!indexingSuccess)
            {
                return await MarkIndexingFailedAsync(vectorDoc!, "Qdrant indexing failed", PdfIndexingErrorCode.QdrantIndexingFailed, cancellationToken).ConfigureAwait(false);
            }

            // Step 4: Save text chunks to PostgreSQL for hybrid search
            await SaveTextChunksToPostgresAsync(pdfId, pdf.GameId, documentChunks!, cancellationToken).ConfigureAwait(false);

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
        // Retrieve PDF document
        var pdf = await _db.PdfDocuments
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

        if (!string.Equals(pdf.ProcessingStatus, "completed", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("PDF {PdfId} processing status is {Status}, expected 'completed'",
                pdfId, pdf.ProcessingStatus);
            return (false, pdf, null, "PDF text extraction not completed", PdfIndexingErrorCode.TextExtractionRequired);
        }

        // Check if already indexed (for idempotency)
        var pdfGuid = Guid.Parse(pdfId);
        var existingVectorDoc = await _db.Set<VectorDocumentEntity>()
            .FirstOrDefaultAsync(v => v.PdfDocumentId == pdfGuid, cancellationToken).ConfigureAwait(false);

        if (existingVectorDoc != null)
        {
            _logger.LogInformation("PDF {PdfId} already indexed, deleting old vectors before re-indexing", pdfId);

            // Delete old vectors from Qdrant
            var deleteResult = await _qdrantService.DeleteDocumentAsync(pdfId, cancellationToken).ConfigureAwait(false);
            if (!deleteResult)
            {
                _logger.LogWarning("Failed to delete old vectors for PDF {PdfId}, continuing anyway", pdfId);
            }

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

        // Generate embeddings
        _logger.LogInformation("Generating embeddings for {ChunkCount} chunks", textChunks.Count);

        var texts = textChunks.Select(c => c.Text).ToList();
        var embeddingResult = await _embeddingService.GenerateEmbeddingsAsync(texts, cancellationToken).ConfigureAwait(false);

        if (!embeddingResult.Success || embeddingResult.Embeddings.Count == 0)
        {
            _logger.LogError("Failed to generate embeddings for PDF {PdfId}: {Error}",
                pdfId, embeddingResult.ErrorMessage);
            return (false, null, $"Embedding generation failed: {embeddingResult.ErrorMessage}", PdfIndexingErrorCode.EmbeddingFailed);
        }

        if (embeddingResult.Embeddings.Count != textChunks.Count)
        {
            _logger.LogError("Embedding count mismatch: expected {Expected}, got {Actual}",
                textChunks.Count, embeddingResult.Embeddings.Count);
            return (false, null, "Embedding count mismatch", PdfIndexingErrorCode.EmbeddingFailed);
        }

        // Prepare document chunks with embeddings
        var documentChunks = textChunks
            .Select((chunk, index) => new DocumentChunk
            {
                Text = chunk.Text,
                Embedding = embeddingResult.Embeddings[index],
                Page = chunk.Page,
                CharStart = chunk.CharStart,
                CharEnd = chunk.CharEnd
            })
            .ToList();

        return (true, documentChunks, null, null);
    }

    /// <summary>
    /// Indexes document chunks in Qdrant and updates VectorDocument.
    /// </summary>
    private async Task<bool> IndexChunksInVectorStoreAsync(
        string pdfId,
        string gameId,
        string extractedText,
        List<DocumentChunk> documentChunks,
        VectorDocumentEntity vectorDoc,
        CancellationToken cancellationToken)
    {
        // Index in Qdrant
        _logger.LogInformation("Indexing {ChunkCount} chunks in Qdrant for PDF {PdfId}",
            documentChunks.Count, pdfId);

        var indexResult = await _qdrantService.IndexDocumentChunksAsync(
            gameId,
            pdfId,
            documentChunks,
            cancellationToken).ConfigureAwait(false);

        if (!indexResult.Success)
        {
            _logger.LogError("Failed to index chunks in Qdrant for PDF {PdfId}: {Error}",
                pdfId, indexResult.ErrorMessage);
            return false;
        }

        // Update VectorDocumentEntity to "completed"
        vectorDoc.IndexingStatus = "completed";
        vectorDoc.ChunkCount = documentChunks.Count;
        vectorDoc.TotalCharacters = extractedText.Length;
        vectorDoc.IndexedAt = _timeProvider.GetUtcNow().UtcDateTime;
        vectorDoc.IndexingError = null;

        return true;
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


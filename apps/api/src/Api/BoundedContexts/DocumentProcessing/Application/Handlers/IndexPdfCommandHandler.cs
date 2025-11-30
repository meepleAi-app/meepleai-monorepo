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
public class IndexPdfCommandHandler : ICommandHandler<IndexPdfCommand, IndexingResultDto>
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
        var pdfId = command.PdfId;
        _logger.LogInformation("Starting indexing for PDF {PdfId}", pdfId);

        // 1. Retrieve PDF document
        var pdf = await _db.PdfDocuments
            .Include(p => p.Game)
            .FirstOrDefaultAsync(p => p.Id.ToString() == pdfId, cancellationToken);

        if (pdf == null)
        {
            _logger.LogWarning("PDF {PdfId} not found", pdfId);
            return IndexingResultDto.CreateFailure("PDF not found", PdfIndexingErrorCode.PdfNotFound);
        }

        // 2. Validate text extraction is complete
        if (string.IsNullOrWhiteSpace(pdf.ExtractedText))
        {
            _logger.LogWarning("PDF {PdfId} has no extracted text", pdfId);
            return IndexingResultDto.CreateFailure(
                "PDF text extraction required. Please extract text before indexing.",
                PdfIndexingErrorCode.TextExtractionRequired);
        }

        if (!string.Equals(pdf.ProcessingStatus, "completed", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("PDF {PdfId} processing status is {Status}, expected 'completed'",
                pdfId, pdf.ProcessingStatus);
            return IndexingResultDto.CreateFailure(
                "PDF text extraction not completed",
                PdfIndexingErrorCode.TextExtractionRequired);
        }

        // 3. Check if already indexed (for idempotency)
        var pdfGuid = Guid.Parse(pdfId);
        var existingVectorDoc = await _db.Set<VectorDocumentEntity>()
            .FirstOrDefaultAsync(v => v.PdfDocumentId == pdfGuid, cancellationToken);

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
            // Create new VectorDocumentEntity with actual embedding configuration
            // Get actual dimensions from embedding service to ensure consistency
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

        try
        {
            // 4. Chunk the text
            _logger.LogInformation("Chunking text for PDF {PdfId} ({CharCount} characters)",
                pdfId, pdf.ExtractedText.Length);

            var textChunks = _chunkingService.ChunkText(pdf.ExtractedText);

            if (textChunks.Count == 0)
            {
                _logger.LogWarning("No chunks created for PDF {PdfId}", pdfId);
                return await MarkIndexingFailedAsync(existingVectorDoc,
                    "No chunks created from text",
                    PdfIndexingErrorCode.ChunkingFailed, cancellationToken);
            }

            _logger.LogInformation("Created {ChunkCount} chunks for PDF {PdfId}", textChunks.Count, pdfId);

            // 5. Generate embeddings
            _logger.LogInformation("Generating embeddings for {ChunkCount} chunks", textChunks.Count);

            var texts = textChunks.Select(c => c.Text).ToList();
            var embeddingResult = await _embeddingService.GenerateEmbeddingsAsync(texts, cancellationToken).ConfigureAwait(false);

            if (!embeddingResult.Success || embeddingResult.Embeddings.Count == 0)
            {
                _logger.LogError("Failed to generate embeddings for PDF {PdfId}: {Error}",
                    pdfId, embeddingResult.ErrorMessage);
                return await MarkIndexingFailedAsync(existingVectorDoc,
                    $"Embedding generation failed: {embeddingResult.ErrorMessage}",
                    PdfIndexingErrorCode.EmbeddingFailed, cancellationToken);
            }

            if (embeddingResult.Embeddings.Count != textChunks.Count)
            {
                _logger.LogError("Embedding count mismatch: expected {Expected}, got {Actual}",
                    textChunks.Count, embeddingResult.Embeddings.Count);
                return await MarkIndexingFailedAsync(existingVectorDoc,
                    "Embedding count mismatch",
                    PdfIndexingErrorCode.EmbeddingFailed, cancellationToken);
            }

            // 6. Prepare document chunks with embeddings
            var documentChunks = new List<DocumentChunk>();
            for (int i = 0; i < textChunks.Count; i++)
            {
                documentChunks.Add(new DocumentChunk
                {
                    Text = textChunks[i].Text,
                    Embedding = embeddingResult.Embeddings[i],
                    Page = textChunks[i].Page,
                    CharStart = textChunks[i].CharStart,
                    CharEnd = textChunks[i].CharEnd
                });
            }

            // 7. Index in Qdrant
            _logger.LogInformation("Indexing {ChunkCount} chunks in Qdrant for PDF {PdfId}",
                documentChunks.Count, pdfId);

            var indexResult = await _qdrantService.IndexDocumentChunksAsync(
                pdf.GameId.ToString(),
                pdfId,
                documentChunks,
                cancellationToken);

            if (!indexResult.Success)
            {
                _logger.LogError("Failed to index chunks in Qdrant for PDF {PdfId}: {Error}",
                    pdfId, indexResult.ErrorMessage);
                return await MarkIndexingFailedAsync(existingVectorDoc,
                    $"Qdrant indexing failed: {indexResult.ErrorMessage}",
                    PdfIndexingErrorCode.QdrantIndexingFailed, cancellationToken);
            }

            // 8. Update VectorDocumentEntity to "completed"
            existingVectorDoc.IndexingStatus = "completed";
            existingVectorDoc.ChunkCount = documentChunks.Count;
            existingVectorDoc.TotalCharacters = pdf.ExtractedText.Length;
            existingVectorDoc.IndexedAt = _timeProvider.GetUtcNow().UtcDateTime;
            existingVectorDoc.IndexingError = null;

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Successfully indexed PDF {PdfId}: {ChunkCount} chunks, {TotalChars} characters",
                pdfId, documentChunks.Count, pdf.ExtractedText.Length);

            return IndexingResultDto.CreateSuccess(
                existingVectorDoc.Id.ToString(),
                documentChunks.Count,
                existingVectorDoc.IndexedAt.Value);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Service boundary - error state management for complex multi-system operation
        // PDF indexing involves multiple external systems (Qdrant, DB, file system) that must maintain consistency
        catch (Exception ex)
        {
            // ERROR STATE MANAGEMENT: Top-level catch ensures graceful failure handling
            // Rationale: PDF indexing involves multiple external systems (Qdrant, DB, file system).
            // Any unhandled error should be captured, logged, and persisted as a failed indexing
            // attempt rather than throwing to the caller. This maintains data consistency and
            // provides operators with debugging context via the indexing_error field.
            // Context: Covers unforeseen errors after specific exception handlers above
            _logger.LogError(ex, "Unexpected error indexing PDF {PdfId}", pdfId);
            return await MarkIndexingFailedAsync(existingVectorDoc,
                $"Unexpected error: {ex.Message}",
                PdfIndexingErrorCode.UnexpectedError, cancellationToken);
        }
    }

    private async Task<IndexingResultDto> MarkIndexingFailedAsync(
        VectorDocumentEntity vectorDoc,
        string errorMessage,
        PdfIndexingErrorCode errorCode,
        CancellationToken ct)
    {
        vectorDoc.IndexingStatus = "failed";
        vectorDoc.IndexingError = errorMessage;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        return IndexingResultDto.CreateFailure(errorMessage, errorCode);
    }
}

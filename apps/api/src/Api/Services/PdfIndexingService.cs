using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
/// Service for orchestrating PDF document indexing into vector database
/// AI-01: Coordinates the workflow: text extraction → chunking → embedding → Qdrant indexing
/// </summary>
public class PdfIndexingService
{
    private readonly MeepleAiDbContext _db;
    private readonly ITextChunkingService _chunkingService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IQdrantService _qdrantService;
    private readonly ILogger<PdfIndexingService> _logger;

    public PdfIndexingService(
        MeepleAiDbContext db,
        ITextChunkingService chunkingService,
        IEmbeddingService embeddingService,
        IQdrantService qdrantService,
        ILogger<PdfIndexingService> logger)
    {
        _db = db;
        _chunkingService = chunkingService;
        _embeddingService = embeddingService;
        _qdrantService = qdrantService;
        _logger = logger;
    }

    /// <summary>
    /// Index a PDF document for semantic search
    /// </summary>
    /// <param name="pdfId">PDF document identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Indexing result with chunk count and metadata</returns>
    public async Task<PdfIndexingResult> IndexPdfAsync(string pdfId, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting indexing for PDF {PdfId}", pdfId);

        // 1. Retrieve PDF document
        var pdf = await _db.PdfDocuments
            .Include(p => p.Game)
            .FirstOrDefaultAsync(p => p.Id == pdfId, ct);

        if (pdf == null)
        {
            _logger.LogWarning("PDF {PdfId} not found", pdfId);
            return PdfIndexingResult.CreateFailure("PDF not found", PdfIndexingErrorCode.PdfNotFound);
        }

        // 2. Validate text extraction is complete
        if (string.IsNullOrWhiteSpace(pdf.ExtractedText))
        {
            _logger.LogWarning("PDF {PdfId} has no extracted text", pdfId);
            return PdfIndexingResult.CreateFailure(
                "PDF text extraction required. Please extract text before indexing.",
                PdfIndexingErrorCode.TextExtractionRequired);
        }

        if (!string.Equals(pdf.ProcessingStatus, "completed", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("PDF {PdfId} processing status is {Status}, expected 'completed'",
                pdfId, pdf.ProcessingStatus);
            return PdfIndexingResult.CreateFailure(
                "PDF text extraction not completed",
                PdfIndexingErrorCode.TextExtractionRequired);
        }

        // 3. Check if already indexed (for idempotency)
        var existingVectorDoc = await _db.Set<VectorDocumentEntity>()
            .FirstOrDefaultAsync(v => v.PdfDocumentId == pdfId, ct);

        if (existingVectorDoc != null)
        {
            _logger.LogInformation("PDF {PdfId} already indexed, deleting old vectors before re-indexing", pdfId);

            // Delete old vectors from Qdrant
            var deleteResult = await _qdrantService.DeleteDocumentAsync(pdfId, ct);
            if (!deleteResult)
            {
                _logger.LogWarning("Failed to delete old vectors for PDF {PdfId}, continuing anyway", pdfId);
            }

            // Update existing entity status to "processing"
            existingVectorDoc.IndexingStatus = "processing";
            existingVectorDoc.IndexingError = null;
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            // Create new VectorDocumentEntity
            existingVectorDoc = new VectorDocumentEntity
            {
                Id = $"vec-{Guid.NewGuid():N}",
                GameId = pdf.GameId,
                PdfDocumentId = pdfId,
                IndexingStatus = "processing",
                EmbeddingModel = "openai/text-embedding-3-small",
                EmbeddingDimensions = 1536
            };
            _db.Set<VectorDocumentEntity>().Add(existingVectorDoc);
            await _db.SaveChangesAsync(ct);
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
                    PdfIndexingErrorCode.ChunkingFailed, ct);
            }

            _logger.LogInformation("Created {ChunkCount} chunks for PDF {PdfId}", textChunks.Count, pdfId);

            // 5. Generate embeddings
            _logger.LogInformation("Generating embeddings for {ChunkCount} chunks", textChunks.Count);

            var texts = textChunks.Select(c => c.Text).ToList();
            var embeddingResult = await _embeddingService.GenerateEmbeddingsAsync(texts, ct);

            if (!embeddingResult.Success || embeddingResult.Embeddings.Count == 0)
            {
                _logger.LogError("Failed to generate embeddings for PDF {PdfId}: {Error}",
                    pdfId, embeddingResult.ErrorMessage);
                return await MarkIndexingFailedAsync(existingVectorDoc,
                    $"Embedding generation failed: {embeddingResult.ErrorMessage}",
                    PdfIndexingErrorCode.EmbeddingFailed, ct);
            }

            if (embeddingResult.Embeddings.Count != textChunks.Count)
            {
                _logger.LogError("Embedding count mismatch: expected {Expected}, got {Actual}",
                    textChunks.Count, embeddingResult.Embeddings.Count);
                return await MarkIndexingFailedAsync(existingVectorDoc,
                    "Embedding count mismatch",
                    PdfIndexingErrorCode.EmbeddingFailed, ct);
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
                pdf.GameId,
                pdfId,
                documentChunks,
                ct);

            if (!indexResult.Success)
            {
                _logger.LogError("Failed to index chunks in Qdrant for PDF {PdfId}: {Error}",
                    pdfId, indexResult.ErrorMessage);
                return await MarkIndexingFailedAsync(existingVectorDoc,
                    $"Qdrant indexing failed: {indexResult.ErrorMessage}",
                    PdfIndexingErrorCode.QdrantIndexingFailed, ct);
            }

            // 8. Update VectorDocumentEntity to "completed"
            existingVectorDoc.IndexingStatus = "completed";
            existingVectorDoc.ChunkCount = documentChunks.Count;
            existingVectorDoc.TotalCharacters = pdf.ExtractedText.Length;
            existingVectorDoc.IndexedAt = DateTime.UtcNow;
            existingVectorDoc.IndexingError = null;

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Successfully indexed PDF {PdfId}: {ChunkCount} chunks, {TotalChars} characters",
                pdfId, documentChunks.Count, pdf.ExtractedText.Length);

            return PdfIndexingResult.CreateSuccess(
                existingVectorDoc.Id,
                documentChunks.Count,
                existingVectorDoc.IndexedAt.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error indexing PDF {PdfId}", pdfId);
            return await MarkIndexingFailedAsync(existingVectorDoc,
                $"Unexpected error: {ex.Message}",
                PdfIndexingErrorCode.UnexpectedError, ct);
        }
    }

    private async Task<PdfIndexingResult> MarkIndexingFailedAsync(
        VectorDocumentEntity vectorDoc,
        string errorMessage,
        PdfIndexingErrorCode errorCode,
        CancellationToken ct)
    {
        vectorDoc.IndexingStatus = "failed";
        vectorDoc.IndexingError = errorMessage;
        await _db.SaveChangesAsync(ct);

        return PdfIndexingResult.CreateFailure(errorMessage, errorCode);
    }
}

/// <summary>
/// Result of PDF indexing operation
/// </summary>
public record PdfIndexingResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public PdfIndexingErrorCode? ErrorCode { get; init; }

    public string? VectorDocumentId { get; init; }
    public int ChunkCount { get; init; }
    public DateTime? IndexedAt { get; init; }

    public static PdfIndexingResult CreateSuccess(string vectorDocumentId, int chunkCount, DateTime indexedAt) =>
        new()
        {
            Success = true,
            VectorDocumentId = vectorDocumentId,
            ChunkCount = chunkCount,
            IndexedAt = indexedAt
        };

    public static PdfIndexingResult CreateFailure(string errorMessage, PdfIndexingErrorCode errorCode) =>
        new()
        {
            Success = false,
            ErrorMessage = errorMessage,
            ErrorCode = errorCode
        };
}

/// <summary>
/// Error codes for PDF indexing failures
/// </summary>
public enum PdfIndexingErrorCode
{
    PdfNotFound,
    TextExtractionRequired,
    ChunkingFailed,
    EmbeddingFailed,
    QdrantIndexingFailed,
    UnexpectedError
}

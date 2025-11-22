namespace Api.BoundedContexts.DocumentProcessing.Application.DTOs;

/// <summary>
/// Result of PDF indexing operation
/// </summary>
public record IndexingResultDto
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public PdfIndexingErrorCode? ErrorCode { get; init; }

    public string? VectorDocumentId { get; init; }
    public int ChunkCount { get; init; }
    public DateTime? IndexedAt { get; init; }

    public static IndexingResultDto CreateSuccess(string vectorDocumentId, int chunkCount, DateTime indexedAt) =>
        new()
        {
            Success = true,
            VectorDocumentId = vectorDocumentId,
            ChunkCount = chunkCount,
            IndexedAt = indexedAt
        };

    public static IndexingResultDto CreateFailure(string errorMessage, PdfIndexingErrorCode errorCode) =>
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

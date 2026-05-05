using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Splits OCR-extracted page text into overlapping chunks suitable for vector embedding.
/// Chunk size: 512 tokens (≈ 2000 chars), 10% overlap.
/// Libro Game AI Assistant MVP Phase 2 — Task 2.3a.
/// </summary>
internal interface IDocumentChunker
{
    IReadOnlyList<KnowledgeChunk> ChunkPage(
        Guid photoBatchUploadId,
        Guid photoBatchPageId,
        int pageNumber,
        string pageText,
        string language,
        float ocrConfidence);
}

using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// Splits OCR page text into overlapping fixed-size chunks.
/// Strategy: character-based sliding window (≈ 512 tokens = 2000 chars) with 10% overlap,
/// breaking at sentence boundaries when possible.
/// Libro Game AI Assistant MVP Phase 2 — Task 2.3a.
/// </summary>
internal sealed class PageTextChunker : IDocumentChunker
{
    private const int ChunkSizeChars = 2000;  // ≈ 512 tokens
    private const int OverlapChars = 200;      // ~10% overlap

    public IReadOnlyList<KnowledgeChunk> ChunkPage(
        Guid photoBatchUploadId,
        Guid photoBatchPageId,
        int pageNumber,
        string pageText,
        string language,
        float ocrConfidence)
    {
        if (string.IsNullOrWhiteSpace(pageText))
            return Array.Empty<KnowledgeChunk>();

        var chunks = new List<KnowledgeChunk>();
        var textLength = pageText.Length;
        var chunkIndex = 0;
        var startOffset = 0;

        while (startOffset < textLength)
        {
            var endOffset = Math.Min(startOffset + ChunkSizeChars, textLength);

            // Try to break at sentence boundary within the last 200 chars of the window
            if (endOffset < textLength)
            {
                var searchStart = Math.Max(startOffset, endOffset - 200);
                var lastPeriod = pageText.LastIndexOf('.', endOffset - 1, endOffset - searchStart);
                if (lastPeriod > startOffset)
                    endOffset = lastPeriod + 1;
            }

            var chunkText = pageText.Substring(startOffset, endOffset - startOffset).Trim();
            if (chunkText.Length > 0)
            {
                chunks.Add(KnowledgeChunk.Create(
                    batchId: photoBatchUploadId,
                    pageId: photoBatchPageId,
                    pageNumber: pageNumber,
                    text: chunkText,
                    chunkIndex: chunkIndex++,
                    startOffset: startOffset,
                    endOffset: endOffset,
                    language: language,
                    confidence: ocrConfidence));
            }

            if (endOffset >= textLength) break;

            // Advance with overlap
            startOffset = Math.Max(0, endOffset - OverlapChars);
        }

        return chunks;
    }
}

namespace Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

/// <summary>
/// Represents a text chunk extracted from a processed photo page,
/// ready for vector embedding and KB indexing.
/// Libro Game AI Assistant MVP Phase 2 — Task 2.3a.
/// </summary>
internal sealed record KnowledgeChunk(
    Guid PhotoBatchUploadId,
    Guid PhotoBatchPageId,
    int PageNumber,
    string TextContent,
    int ChunkIndex,
    int StartCharOffset,
    int EndCharOffset,
    string Language,
    float ConfidenceScore)
{
    public int CharLength => TextContent.Length;

    public static KnowledgeChunk Create(
        Guid batchId,
        Guid pageId,
        int pageNumber,
        string text,
        int chunkIndex,
        int startOffset,
        int endOffset,
        string language,
        float confidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
        if (confidence is < 0f or > 1f) throw new ArgumentOutOfRangeException(nameof(confidence));

        return new KnowledgeChunk(batchId, pageId, pageNumber, text,
            chunkIndex, startOffset, endOffset, language, confidence);
    }
}

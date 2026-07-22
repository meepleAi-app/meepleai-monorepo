using Api.Constants;
using Api.Services;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services.Chunking;

/// <summary>
/// Bridges <see cref="HeadingAwareChunker"/>'s <see cref="DocumentChunk"/> output to the
/// <see cref="DocumentChunkInput"/> the fresh-upload ingest handlers embed + persist, and
/// caps embedding-input text at <see cref="ChunkingConstants.MaxEmbeddingChars"/> (the full
/// text is still persisted for retrieval; only the vector-provider input is capped). Shared
/// by all 3 fresh-ingest paths (Issue #3281) so the map + cap logic lives in one tested place.
/// </summary>
internal static class HeadingAwareChunkAdapter
{
    public static List<DocumentChunkInput> ToChunkInputs(IReadOnlyList<DocumentChunk> chunks)
    {
        ArgumentNullException.ThrowIfNull(chunks);
        return chunks
            .Select(c => new DocumentChunkInput
            {
                Id = c.Id,
                Text = c.Text,
                Page = c.Page,
                CharStart = c.CharStart,
                CharEnd = c.CharEnd,
                Heading = c.Heading,
                Level = c.Level,
                ParentChunkId = c.ParentChunkId,
                ElementType = c.ElementType,
            })
            .ToList();
    }

    public static string CapForEmbedding(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Length <= ChunkingConstants.MaxEmbeddingChars
            ? text
            : text[..ChunkingConstants.MaxEmbeddingChars];
    }
}

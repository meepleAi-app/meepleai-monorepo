using Api.BoundedContexts.KnowledgeBase.Domain.Chunking;
using Api.Services;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;

/// <summary>
/// Slice D: pure adapter mapping <see cref="HierarchicalChunk"/> (chunking pipeline output,
/// parents Level 0 + children Level 2) to <see cref="DocumentChunk"/> (vector search /
/// persistence model), so the existing sink can store Heading/Level/ParentChunkId/ElementType.
/// No side effects, no embedding computation — <see cref="DocumentChunk.Embedding"/> is filled
/// later by the caller.
/// </summary>
internal static class HierarchicalChunkMapper
{
    /// <summary>
    /// Maps a flat list of <see cref="HierarchicalChunk"/> (parents and their children) to
    /// <see cref="DocumentChunk"/>, preserving parent/child identity via <c>Guid.ParseExact("N")</c>
    /// round-trip of the ids <c>AdvancedChunkingService</c> emits.
    /// </summary>
    public static List<DocumentChunk> ToDocumentChunks(IReadOnlyList<HierarchicalChunk> chunks)
    {
        ArgumentNullException.ThrowIfNull(chunks);

        var result = new List<DocumentChunk>(chunks.Count);

        foreach (var chunk in chunks)
        {
            result.Add(new DocumentChunk
            {
                Id = Guid.ParseExact(chunk.Id, "N"),
                ParentChunkId = string.IsNullOrEmpty(chunk.ParentId)
                    ? null
                    : Guid.ParseExact(chunk.ParentId, "N"),
                Level = (short)chunk.Level,
                Heading = chunk.Metadata.Heading,
                ElementType = string.IsNullOrEmpty(chunk.Metadata.ElementType)
                    ? "NarrativeText"
                    : chunk.Metadata.ElementType,
                Text = chunk.Content,
                Page = chunk.Metadata.Page,
                CharStart = chunk.Metadata.CharStart,
                CharEnd = chunk.Metadata.CharEnd,
                BBox = chunk.Metadata.BBox,  // SP-B (#3406): carry the region to the sink
                Embedding = Array.Empty<float>()
            });
        }

        return result;
    }
}

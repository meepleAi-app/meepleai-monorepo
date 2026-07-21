using Api.BoundedContexts.KnowledgeBase.Domain.Chunking;
using Api.Services;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services.Chunking;

/// <summary>
/// SP2: maps AdvancedChunkingService output to embeddable inputs, keeping ONLY child chunks
/// (Level 2). Heading is already inherited from the parent section in the child's ChunkMetadata.
/// Parent (Level 0) chunks are not persisted; ParentChunkId is left null (only-child model).
/// </summary>
internal static class HierarchicalChunkMapper
{
    public static List<DocumentChunkInput> ToChildDocumentChunks(IReadOnlyList<HierarchicalChunk> chunks)
    {
        var result = new List<DocumentChunkInput>();
        if (chunks is null)
        {
            return result;
        }

        foreach (var c in chunks)
        {
            if (c.IsRoot)
            {
                continue; // parent/section container is not persisted
            }
            if (string.IsNullOrWhiteSpace(c.Content))
            {
                continue;
            }

            result.Add(new DocumentChunkInput
            {
                Text = c.Content,
                // Recompute per-child page from CharStart (~2000 chars/page, matching
                // TextChunkingService.EstimatePageNumber) so a multi-page section does not collapse
                // every child to the section's first page. Falls back to the section page for offset 0.
                Page = c.Metadata.CharStart > 0 ? (c.Metadata.CharStart / 2000) + 1 : c.Metadata.Page,
                CharStart = c.Metadata.CharStart,
                CharEnd = c.Metadata.CharEnd,
                Heading = c.Metadata.Heading,
                Level = 2,
                ParentChunkId = null,
                ElementType = string.IsNullOrWhiteSpace(c.Metadata.ElementType) ? "text" : c.Metadata.ElementType,
            });
        }

        return result;
    }
}

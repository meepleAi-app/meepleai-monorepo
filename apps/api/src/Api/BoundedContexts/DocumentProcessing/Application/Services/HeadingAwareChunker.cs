using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;
using Api.Services;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Slice D: shared helper turning a structured extraction (or its flat-text fallback) into
/// heading-bearing <see cref="DocumentChunk"/>s, reused by every ingest path. Chains
/// <see cref="ExtractedDocumentFactory.FromExtraction"/> (builds sections from Title elements,
/// falling back to a single null-heading section when no structured elements are available) →
/// <see cref="IAdvancedChunkingService.ChunkDocumentAsync"/> (parent/child hierarchical chunking) →
/// <see cref="HierarchicalChunkMapper.ToDocumentChunks"/> (adapts to the persistence model).
/// Returned chunks carry <c>Embedding = Array.Empty&lt;float&gt;()</c> — the caller batch-embeds
/// <c>chunk.Text</c> afterward.
/// </summary>
internal static class HeadingAwareChunker
{
    public static async Task<List<DocumentChunk>> BuildAsync(
        IReadOnlyList<ExtractedElement>? structuredElements,
        string flatText,
        Guid documentId,
        Guid? gameId,
        IAdvancedChunkingService advancedChunking,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(advancedChunking);

        var extractedDocument = ExtractedDocumentFactory.FromExtraction(
            documentId,
            gameId,
            structuredElements,
            flatText);

        var hierarchicalChunks = await advancedChunking
            .ChunkDocumentAsync(extractedDocument, config: null, cancellationToken)
            .ConfigureAwait(false);

        return HierarchicalChunkMapper.ToDocumentChunks(hierarchicalChunks);
    }
}

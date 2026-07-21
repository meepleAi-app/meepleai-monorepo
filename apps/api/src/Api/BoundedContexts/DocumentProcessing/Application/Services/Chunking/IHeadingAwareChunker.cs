using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;
using Api.Constants;
using Api.Services;

#pragma warning disable MA0048 // File name must match type name — interface + impl together
namespace Api.BoundedContexts.DocumentProcessing.Application.Services.Chunking;

internal interface IHeadingAwareChunker
{
    Task<List<DocumentChunkInput>> ChunkAsync(
        Guid documentId,
        Guid? gameId,
        IReadOnlyList<ExtractedElement>? structuredElements,
        string fullText,
        CancellationToken ct);
}

/// <summary>
/// SP2: chains the heading-aware chunking pipeline — ExtractedDocumentFactory (SP1) builds
/// heading-scoped sections, AdvancedChunkingService (auto-strategy) chunks them hierarchically,
/// HierarchicalChunkMapper (SP2 task 1) flattens to embeddable children, and a post-split pass
/// re-splits any child exceeding MaxEmbeddingChars — the Sparse strategy can emit ~2000-char
/// children that would otherwise be silently truncated at embedding time.
/// </summary>
internal sealed class HeadingAwareChunker : IHeadingAwareChunker
{
    private readonly IAdvancedChunkingService _advanced;
    private readonly ITextChunkingService _textChunking;
    private readonly ILogger<HeadingAwareChunker> _logger;

    public HeadingAwareChunker(
        IAdvancedChunkingService advanced,
        ITextChunkingService textChunking,
        ILogger<HeadingAwareChunker> logger)
    {
        _advanced = advanced;
        _textChunking = textChunking;
        _logger = logger;
    }

    public async Task<List<DocumentChunkInput>> ChunkAsync(
        Guid documentId, Guid? gameId,
        IReadOnlyList<ExtractedElement>? structuredElements,
        string fullText, CancellationToken ct)
    {
        var document = ExtractedDocumentFactory.FromExtraction(documentId, gameId, structuredElements, fullText ?? string.Empty);
        var hchunks = await _advanced.ChunkDocumentAsync(document, config: null, ct).ConfigureAwait(false);
        var mapped = HierarchicalChunkMapper.ToChildDocumentChunks(hchunks);
        var result = PostSplitOversized(mapped);

        _logger.LogDebug(
            "HeadingAwareChunker produced {ChunkCount} children for document {DocumentId} ({SectionCount} sections)",
            result.Count, documentId, document.Sections.Count);

        return result;
    }

    // Mirror of EnhancedPdfProcessingOrchestrator.SplitOversizedPageChunks: no embedded chunk may exceed
    // MaxEmbeddingChars (E5-base token limit); Sparse strategy can emit ~2000-char children.
    private List<DocumentChunkInput> PostSplitOversized(List<DocumentChunkInput> chunks)
    {
        var result = new List<DocumentChunkInput>(chunks.Count);
        foreach (var chunk in chunks)
        {
            if (chunk.Text.Length <= ChunkingConstants.MaxEmbeddingChars)
            {
                result.Add(chunk);
                continue;
            }

            var subs = _textChunking.ChunkText(chunk.Text, ChunkingConstants.MaxEmbeddingChars, ChunkingConstants.DefaultChunkOverlap);
            foreach (var sub in subs.Where(s => !string.IsNullOrWhiteSpace(s.Text)))
            {
                result.Add(chunk with
                {
                    Text = sub.Text,
                    CharStart = chunk.CharStart + sub.CharStart,
                    CharEnd = chunk.CharStart + sub.CharEnd,
                });
            }
        }
        return result;
    }
}

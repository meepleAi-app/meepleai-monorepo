using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;
using Api.Constants;
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
    /// <summary>Builds the heading-bearing chunks of one document (see the class summary).</summary>
    /// <param name="structuredElements">The raw extraction elements; null or empty when the extractor gave none.</param>
    /// <param name="flatText">The extracted text, used when there are no structured elements.</param>
    /// <param name="documentId">The document the chunks belong to.</param>
    /// <param name="gameId">The game the document belongs to, if any.</param>
    /// <param name="advancedChunking">The hierarchical chunker.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <param name="pageSpans">
    /// Where each page begins in <paramref name="flatText"/>, when the extractor measured its pages (#4782). Without
    /// structured elements the document is flat, and this is the only thing that tells its pages apart.
    /// </param>
    public static async Task<List<DocumentChunk>> BuildAsync(
        IReadOnlyList<ExtractedElement>? structuredElements,
        string flatText,
        Guid documentId,
        Guid? gameId,
        IAdvancedChunkingService advancedChunking,
        CancellationToken cancellationToken,
        IReadOnlyList<PageSpan>? pageSpans = null)
    {
        ArgumentNullException.ThrowIfNull(advancedChunking);

        // Epic #3338 WP1a: recover section titles that unstructured's 'fast' strategy glued into a
        // body element instead of emitting as a standalone Title (the TM "PREPARAZIONE" case). Runs
        // BEFORE FromExtraction so GroupByTitle sees the promoted synthetic Title and opens a real
        // section — otherwise the setup content stays under the previous section's heading ("CARTE")
        // and the #3270 heading boost + RoleClassifier Setup fast-path never fire.
        var elements = structuredElements is null
            ? null
            : EmbeddedTitleSplitter.Split(structuredElements);

        var extractedDocument = ExtractedDocumentFactory.FromExtraction(
            documentId,
            gameId,
            elements,
            flatText,
            pageSpans);

        var hierarchicalChunks = await advancedChunking
            .ChunkDocumentAsync(extractedDocument, config: null, cancellationToken)
            .ConfigureAwait(false);

        var chunks = HierarchicalChunkMapper.ToDocumentChunks(hierarchicalChunks);

        // #3269 fragment filter: drop decorative/vertical-text artefacts ("A N", "N",
        // "I L E X Y R F") that unstructured emits as bogus Title elements on complex-layout PDFs.
        // They pollute retrieval — a query token like "N" ("N giocatori") matches such micro-chunks
        // with a very high ts_rank (short chunk => high normalized rank), burying the real section
        // chunk. Keep only chunks whose Text carries a real word (a run of >= MinSubstantiveLetterRun
        // consecutive letters). The criterion is absolute and monotone across the parent⊇child
        // hierarchy (a parent concatenates its children), so filtering never orphans a child.
        return chunks.Where(IsSubstantial).ToList();
    }

    /// <summary>
    /// True when <paramref name="chunk"/> carries at least one run of
    /// <see cref="ChunkingConstants.MinSubstantiveLetterRun"/> consecutive Unicode letters — i.e. one
    /// real word. Decorative fragments ("A N", "I L E X Y R F", digit/symbol-only runs) have none and
    /// are dropped. Single scan (no regex) — allocation-free and DoS-safe.
    /// </summary>
    internal static bool IsSubstantial(DocumentChunk chunk)
    {
        var text = chunk.Text;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var run = 0;
        foreach (var ch in text)
        {
            if (char.IsLetter(ch))
            {
                if (++run >= ChunkingConstants.MinSubstantiveLetterRun)
                {
                    return true;
                }
            }
            else
            {
                run = 0;
            }
        }

        return false;
    }
}

using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query that retrieves the extracted OCR text for a single page of a scanned rulebook
/// photo batch.
///
/// Strategy (two-step):
/// 1. Numbered lookup — returns the page's <c>ExtractedText</c> from <c>photo_batch_pages</c>
///    when available. The lookup key (<see cref="LookupKey"/>) decides whether the search
///    targets the physical page index or the narrative paragraph number extracted from OCR.
/// 2. Semantic fallback — when numbered text is absent (page not yet indexed or blank),
///    a hybrid RAG search is performed using <see cref="SemanticFallbackHint"/> (or a
///    generic hint) to surface the closest relevant passage from the game's knowledge base.
///
/// Authorization: only the owner of the batch may read its pages.
/// Libro Game AI Assistant MVP Phase 3 — G4. Issue #747 PR-B: lookup key discriminator.
/// </summary>
/// <param name="PhotoBatchUploadId">The photo batch upload to read from.</param>
/// <param name="LookupKey">Discriminator selecting the lookup strategy (page vs. paragraph).</param>
/// <param name="UserId">The authenticated user making the request (used for ownership check).</param>
/// <param name="SemanticFallbackHint">
/// Optional hint for the semantic fallback search (e.g. a partial page title or keyword).
/// When null or whitespace a generic hint derived from <see cref="LookupKey"/> is used.
/// </param>
internal sealed record GetParagraphQuery(
    Guid PhotoBatchUploadId,
    ParagraphLookupKey LookupKey,
    Guid UserId,
    string? SemanticFallbackHint = null
) : IQuery<ParagraphDto>
{
    /// <summary>
    /// Factory for the legacy <see cref="ParagraphLookupKey.ByPageNumber"/> lookup.
    /// Use for callers that index batches by physical photo page.
    /// </summary>
    internal static GetParagraphQuery ByPage(
        Guid photoBatchUploadId,
        int pageNumber,
        Guid userId,
        string? semanticFallbackHint = null)
        => new(
            photoBatchUploadId,
            new ParagraphLookupKey.ByPageNumber(pageNumber),
            userId,
            semanticFallbackHint);

    /// <summary>
    /// Factory for the <see cref="ParagraphLookupKey.ByParagraphNumber"/> lookup introduced
    /// by issue #747. Use for gamebook PDFs whose narrative paragraphs span multiple
    /// physical photos.
    /// </summary>
    internal static GetParagraphQuery ByParagraph(
        Guid photoBatchUploadId,
        int paragraphNumber,
        Guid userId,
        string? semanticFallbackHint = null)
        => new(
            photoBatchUploadId,
            new ParagraphLookupKey.ByParagraphNumber(paragraphNumber),
            userId,
            semanticFallbackHint);
}

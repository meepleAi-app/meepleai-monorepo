using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query that retrieves the extracted OCR text for a single page of a scanned rulebook
/// photo batch.
///
/// Strategy (two-step):
/// 1. Numbered lookup — returns the page's <c>ExtractedText</c> from <c>photo_batch_pages</c>
///    when available.
/// 2. Semantic fallback — when numbered text is absent (page not yet indexed or blank),
///    a hybrid RAG search is performed using <see cref="SemanticFallbackHint"/> (or a
///    generic hint) to surface the closest relevant passage from the game's knowledge base.
///
/// Authorization: only the owner of the batch may read its pages.
/// Libro Game AI Assistant MVP Phase 3 — G4.
/// </summary>
/// <param name="PhotoBatchUploadId">The photo batch upload to read from.</param>
/// <param name="PageNumber">The 1-based page number to retrieve. Must be ≥ 1.</param>
/// <param name="UserId">The authenticated user making the request (used for ownership check).</param>
/// <param name="SemanticFallbackHint">
/// Optional hint for the semantic fallback search (e.g. a partial page title or keyword).
/// When null or whitespace a generic "page N content" hint is used.
/// </param>
internal sealed record GetParagraphQuery(
    Guid PhotoBatchUploadId,
    int PageNumber,
    Guid UserId,
    string? SemanticFallbackHint = null
) : IQuery<ParagraphDto>;

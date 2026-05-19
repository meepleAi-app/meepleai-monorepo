namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// Response DTO for <see cref="Queries.GetParagraphQuery"/>.
/// Carries the extracted text for a single scanned rulebook page, together with
/// metadata indicating whether the result came from direct numbered lookup or a
/// semantic-search fallback.
///
/// <para><b>FallbackMethod values:</b></para>
/// <list type="bullet">
///   <item><c>null</c> — no fallback; numbered lookup succeeded.</item>
///   <item><c>"semantic"</c> — vector/hybrid search was used as fallback.</item>
/// </list>
///
/// Libro Game AI Assistant MVP Phase 3 — G4. Issue #747 PR-B: <c>ParagraphNumber</c>
/// surfaces the narrative paragraph identifier when the caller used
/// <see cref="Queries.ParagraphLookupKey.ByParagraphNumber"/>.
/// </summary>
/// <param name="PageNumber">
/// The 1-based physical page number that holds (or was queried for) the returned text.
/// For <see cref="Queries.ParagraphLookupKey.ByPageNumber"/> this echoes the requested
/// page; for <see cref="Queries.ParagraphLookupKey.ByParagraphNumber"/> this is the page
/// that contains the matching paragraph (or <c>0</c> when the numbered lookup missed
/// and the response carries the semantic fallback text).
/// </param>
/// <param name="Text">
/// The text extracted for this page.
/// May be empty when the semantic fallback also yields no results.
/// </param>
/// <param name="FallbackUsed">
/// <c>true</c> when the numbered lookup found no text and the handler fell back to
/// semantic search (or the semantic search itself failed gracefully).
/// </param>
/// <param name="FallbackMethod">
/// The fallback strategy that was used (<c>null</c> or <c>"semantic"</c>).
/// </param>
/// <param name="ParagraphNumber">
/// The narrative paragraph number that the caller requested. Set only when the lookup
/// key was <see cref="Queries.ParagraphLookupKey.ByParagraphNumber"/>; <c>null</c> for
/// page-based lookups so existing clients see no schema change.
/// </param>
internal sealed record ParagraphDto(
    int PageNumber,
    string Text,
    bool FallbackUsed,
    string? FallbackMethod,
    int? ParagraphNumber = null
);

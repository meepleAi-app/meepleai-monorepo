namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Discriminator for the way <see cref="GetParagraphQuery"/> selects a page within
/// a photo batch. Two implementations exist:
/// <list type="bullet">
///   <item><see cref="ByPageNumber"/> — physical photo index (1..N within the batch).
///         This is the legacy lookup mode preserved for callers that scan rulebooks
///         where 1 photo ≈ 1 page.</item>
///   <item><see cref="ByParagraphNumber"/> — narrative paragraph identifier extracted
///         from OCR text (issue #747). Required for gamebook PDFs where a single
///         photo can span multiple paragraphs (Tainted Grail, ISS Vanguard, Nanolith).</item>
/// </list>
/// </summary>
/// <remarks>
/// New lookup modes (e.g. ByEntryNumber for branching gamebooks) should add a new
/// sealed record here. <see cref="GetParagraphQueryHandler"/> exhaustively switches on
/// the concrete type at dispatch time; the compiler will flag missing arms.
/// </remarks>
internal abstract record ParagraphLookupKey
{
    /// <summary>
    /// Lookup by the physical 1-based page index within the photo batch.
    /// </summary>
    /// <param name="PageNumber">The 1-based page index. Must be ≥ 1.</param>
    internal sealed record ByPageNumber(int PageNumber) : ParagraphLookupKey;

    /// <summary>
    /// Lookup by the narrative paragraph identifier extracted from OCR text.
    /// </summary>
    /// <param name="ParagraphNumber">The narrative paragraph number. Must be ≥ 1.</param>
    internal sealed record ByParagraphNumber(int ParagraphNumber) : ParagraphLookupKey;
}

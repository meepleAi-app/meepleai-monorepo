namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Extracts narrative paragraph identifiers from OCR text.
///
/// Issue #747 PR-C: gamebook PDFs number their paragraphs internally (1..N
/// narrative-sequential), distinct from the physical photo page index. A single
/// photo can span multiple narrative paragraphs (Tainted Grail, ISS Vanguard,
/// Nanolith spreads carry ~2-3 paragraphs each), so the extracted IDs are stored
/// as an array on <c>PhotoBatchPage.ParagraphNumbers</c> and queried via
/// <c>IPhotoBatchUploadRepository.GetPageTextByParagraphNumberAsync</c>.
///
/// Implementations should be:
/// <list type="bullet">
///   <item><b>Multi-format tolerant</b>: gamebook publishers use different
///         numbering conventions (<c>42.</c>, <c>Paragrafo 42</c>, <c>§ 42</c>);
///         the extractor combines regex patterns rather than a single rule.</item>
///   <item><b>Whole-line anchored</b>: a numeric token in the middle of a
///         sentence ("retreat 3 hexes") is NOT a paragraph header.</item>
///   <item><b>Deterministic</b>: returns paragraph numbers ordered ascending,
///         duplicates removed.</item>
/// </list>
/// </summary>
internal interface IParagraphNumberExtractor
{
    /// <summary>
    /// Scans <paramref name="ocrText"/> and returns the narrative paragraph
    /// numbers found. Returns an empty array when the text is null, whitespace,
    /// or carries no recognizable paragraph header.
    /// </summary>
    /// <param name="ocrText">Raw OCR output for a single photo page.</param>
    /// <returns>Distinct paragraph numbers ordered ascending.</returns>
    int[] Extract(string? ocrText);
}

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Services;

/// <summary>
/// Serializable snapshot of OCR + lang detection results for a photo, cached by photoId.
/// DEC-8 (#1559): Single-call cache lookup returns OCR + lang detection together.
/// Stored via IHybridCacheService with key from GamebookCacheKeys.PhotoOcr(photoId), TTL 24h.
///
/// NOTE: Confidence is double? (matching domain entity GamebookPhotoArtifact.LangDetectionConfidence).
/// </summary>
internal sealed record PhotoOcrCacheValue(
    string FullText,
    IReadOnlyList<OcrParagraphCacheEntry> Paragraphs,
    double AverageConfidence,
    string? DetectedSourceLang,
    double? LangDetectionConfidence);

internal sealed record OcrParagraphCacheEntry(int Number, string Text);

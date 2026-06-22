namespace Api.BoundedContexts.SessionTracking.Infrastructure.Services;

/// <summary>
/// Result of running NLP language detection on OCR text.
/// DEC-3 (#1559 spec-panel): Lang is one of EN, FR, DE, ES, IT (ISO 639-1 uppercase), or null if
/// outside allowlist or text was too short / ambiguous.
/// DEC-4: Confidence is the raw normalized [0,1] score from the detection library — NOT clipped
/// to a threshold. FE decides UX tier based on the value; see NormalizeConfidence doc in
/// NTextCatLanguageDetectionService for empirical calibration of Core14 score distribution.
/// </summary>
internal sealed record LanguageDetectionResult(string? Lang, double Confidence);

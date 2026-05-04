using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Contract for the smoldocling /preprocess HTTP endpoint adapter.
/// Libro Game AI Assistant MVP Phase 1 — Task 1.4a.
/// Implementation (SmoldoclingPhotoPreprocessor) is deferred to Task 1.4b.
/// </summary>
public interface IPhotoPreprocessor
{
    Task<PhotoPreprocessResult> PreprocessAsync(byte[] imageData, CancellationToken ct = default);
}

/// <summary>
/// Result returned by the /preprocess smoldocling endpoint.
/// </summary>
/// <param name="ProcessedImage">Dewarped/corrected image bytes (JPEG).</param>
/// <param name="ExtractedText">Text extracted by SmolDocling VLM from the preprocessed image.</param>
/// <param name="ConfidenceScore">Estimated OCR confidence (0.0–1.0). SmolDocling does not expose
///     token-level confidence; a heuristic baseline of 0.85 is used when text is extracted.</param>
/// <param name="DetectedOrientation">Physical orientation inferred from image dimensions + EXIF.</param>
/// <param name="IsBlankPage">True when pixel intensity std-dev is below the blank-page threshold.</param>
/// <param name="Warnings">Non-fatal diagnostics (e.g., low-confidence, blurry image, blank page).</param>
public sealed record PhotoPreprocessResult(
    byte[] ProcessedImage,
    string ExtractedText,
    double ConfidenceScore,
    PageOrientation DetectedOrientation,
    bool IsBlankPage,
    string[] Warnings
);

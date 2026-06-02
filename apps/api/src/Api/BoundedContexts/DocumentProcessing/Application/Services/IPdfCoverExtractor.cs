using System.Threading;
using System.Threading.Tasks;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Extracts a "cover image" candidate from the first significant page of a PDF
/// (issue #1831 / umbrella #1821 L4).
/// </summary>
/// <remarks>
/// Implementations scan the first 3 pages, score each by image-density minus
/// normalised text-character-count, pick the highest. When no page clears the
/// minimum image-density threshold the outcome is <see cref="PdfCoverExtractionOutcome.Skipped"/>
/// — caller stores <c>CoverGenerationStatus = Skipped</c> and falls back to L1 placeholder.
/// </remarks>
public interface IPdfCoverExtractor
{
    /// <summary>
    /// Render and select a cover image from the PDF bytes. Returns webp thumb +
    /// preview byte arrays, the page index chosen, or an error/skipped marker.
    /// </summary>
    Task<PdfCoverExtractionResult> ExtractAsync(byte[] pdfBytes, CancellationToken cancellationToken);
}

/// <summary>
/// Outcome of <see cref="IPdfCoverExtractor.ExtractAsync"/>. Discriminated by
/// <see cref="Outcome"/> — callers branch on it before reading the other fields.
/// </summary>
public sealed record PdfCoverExtractionResult
{
    /// <summary>
    /// <c>Generated</c>: <see cref="ThumbnailWebp"/> + <see cref="PreviewWebp"/> + <see cref="SelectedPageIndex"/> populated.
    /// <c>Skipped</c>: heuristic rejected all candidate pages (e.g. text-only legal/intro pages). Bytes are null.
    /// <c>Failed</c>: an exception was caught during rendering or encoding. <see cref="ErrorMessage"/> populated.
    /// </summary>
    public required PdfCoverExtractionOutcome Outcome { get; init; }

    /// <summary>200×300 webp bytes for grid card thumbnail. Null unless <see cref="Outcome"/> is Generated.</summary>
    public byte[]? ThumbnailWebp { get; init; }

    /// <summary>600×900 webp bytes for detail-page preview. Null unless <see cref="Outcome"/> is Generated.</summary>
    public byte[]? PreviewWebp { get; init; }

    /// <summary>0-indexed page chosen by the heuristic. Null unless Generated.</summary>
    public int? SelectedPageIndex { get; init; }

    /// <summary>Diagnostic error string when Outcome=Failed. Surfaced to <c>PdfDocumentEntity.CoverGenerationError</c>.</summary>
    public string? ErrorMessage { get; init; }
}

public enum PdfCoverExtractionOutcome
{
    Generated = 0,
    Skipped = 1,
    Failed = 2,
}

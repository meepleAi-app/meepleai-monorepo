using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Entities;

/// <summary>
/// Represents a single scanned page within a <see cref="PhotoBatchUpload"/>.
/// Stores per-page OCR results, confidence, orientation, and extracted text.
/// Libro Game AI Assistant MVP Phase 1.
/// </summary>
public sealed class PhotoBatchPage : Entity<Guid>
{
    /// <summary>Gets the ID of the owning <see cref="PhotoBatchUpload"/>.</summary>
    public Guid PhotoBatchUploadId { get; private set; }

    /// <summary>Gets the 1-based page number within the batch.</summary>
    public int PageNumber { get; private set; }

    /// <summary>Gets the blob storage key for the source image.</summary>
    public string BlobKey { get; private set; } = null!;

    /// <summary>Gets the raw OCR confidence score (0.0–1.0).</summary>
    public double Confidence { get; private set; }

    /// <summary>Gets the categorical confidence level derived from <see cref="Confidence"/>.</summary>
    public ConfidenceLevel ConfidenceLevel { get; private set; }

    /// <summary>Gets the detected physical orientation of the page.</summary>
    public PageOrientation Orientation { get; private set; }

    /// <summary>Gets whether the page was detected as blank.</summary>
    public bool IsBlank { get; private set; }

    /// <summary>Gets OCR processing warnings for this page.</summary>
    public string[] Warnings { get; private set; } = [];

    /// <summary>Gets the raw text extracted from this page, if available.</summary>
    public string? ExtractedText { get; private set; }

    /// <summary>Gets the UTC timestamp when this page was indexed.</summary>
    public DateTime IndexedAt { get; private set; }

#pragma warning disable CS8618
    private PhotoBatchPage() : base() { } // EF Core
#pragma warning restore CS8618

    /// <summary>
    /// Creates a new <see cref="PhotoBatchPage"/> after OCR processing.
    /// </summary>
    public static PhotoBatchPage Create(
        Guid batchId,
        int pageNumber,
        string blobKey,
        double confidence,
        PageOrientation orientation,
        bool isBlank,
        string[] warnings,
        string? extractedText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blobKey);

        return new PhotoBatchPage
        {
            Id = Guid.NewGuid(),
            PhotoBatchUploadId = batchId,
            PageNumber = pageNumber,
            BlobKey = blobKey,
            Confidence = confidence,
            ConfidenceLevel = ConfidenceLevelExtensions.FromScore(confidence),
            Orientation = orientation,
            IsBlank = isBlank,
            Warnings = warnings ?? [],
            ExtractedText = extractedText,
            IndexedAt = DateTime.UtcNow
        };
    }
}

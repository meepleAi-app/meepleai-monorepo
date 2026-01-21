namespace Api.BoundedContexts.DocumentProcessing.Application.DTOs;

/// <summary>
/// DTO for document preview information.
/// Issue #2732: Document preview for share requests.
/// </summary>
public record DocumentPreviewDto
{
    /// <summary>
    /// Document ID.
    /// </summary>
    public required Guid DocumentId { get; init; }

    /// <summary>
    /// Original file name.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// MIME content type.
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public required long FileSize { get; init; }

    /// <summary>
    /// Time-limited preview URL.
    /// </summary>
    public required string PreviewUrl { get; init; }

    /// <summary>
    /// Number of pages (for PDFs).
    /// </summary>
    public int? PageCount { get; init; }

    /// <summary>
    /// Upload timestamp.
    /// </summary>
    public required DateTime UploadedAt { get; init; }
}

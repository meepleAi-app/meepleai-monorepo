namespace Api.BoundedContexts.DocumentProcessing.Application.DTOs;

/// <summary>
/// DTO returned by <see cref="Queries.GetPhotoBatchStatusQuery"/>.
/// Contains the current processing state of a photo batch plus per-page details.
/// Libro Game AI Assistant MVP Phase 1 — Task 1.7.
/// </summary>
internal sealed record PhotoBatchStatusDto(
    Guid BatchId,
    string Status,
    int TotalPages,
    int IndexedPages,
    int LowConfidencePages,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    PhotoPageStatusDto[] Pages
);

/// <summary>
/// Per-page detail within a <see cref="PhotoBatchStatusDto"/>.
/// </summary>
internal sealed record PhotoPageStatusDto(
    int PageNumber,
    string ThumbnailUrl,
    double Confidence,
    string ConfidenceLevel,
    string[] Warnings
);

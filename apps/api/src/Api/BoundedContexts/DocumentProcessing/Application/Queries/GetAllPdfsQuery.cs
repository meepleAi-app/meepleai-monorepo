using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Query to get all PDF documents with processing status (admin-only).
/// Returns: filename, game, status, chunks, errors for monitoring.
/// PDF Storage Management Hub: Extended with granular state, sorting, and filters.
/// </summary>
internal record GetAllPdfsQuery(
    string? Status = null,
    string? State = null,
    long? MinSizeBytes = null,
    long? MaxSizeBytes = null,
    DateTime? UploadedAfter = null,
    DateTime? UploadedBefore = null,
    Guid? GameId = null,
    string? SortBy = null,
    string? SortOrder = null,
    int PageSize = 50,
    int Page = 1
) : IQuery<PdfListResult>;

/// <summary>
/// Result containing list of PDFs with processing metadata
/// </summary>
internal record PdfListResult(
    List<PdfListItemDto> Items,
    int Total,
    int Page,
    int PageSize
);

/// <summary>
/// Individual PDF item in admin list.
/// PDF Storage Management Hub: Extended with 7-state processing, file size, progress.
/// </summary>
internal record PdfListItemDto(
    Guid Id,
    string FileName,
    string? GameTitle,
    Guid? GameId,
    string ProcessingStatus,
    string ProcessingState,
    int ProgressPercentage,
    long FileSizeBytes,
    int? PageCount,
    int ChunkCount,
    string? ProcessingError,
    string? ErrorCategory,
    int RetryCount,
    DateTime UploadedAt,
    DateTime? ProcessedAt
);

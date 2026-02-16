using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Query to get all PDF documents with processing status (admin-only).
/// Returns: filename, game, status, chunks, errors for monitoring.
/// </summary>
/// <param name="Status">Filter by processing status (optional)</param>
/// <param name="PageSize">Page size (default 50)</param>
/// <param name="Page">Page number (default 1)</param>
internal record GetAllPdfsQuery(
    string? Status = null,
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
/// Individual PDF item in admin list
/// </summary>
internal record PdfListItemDto(
    Guid Id,
    string FileName,
    string? GameTitle,
    Guid? GameId,
    string ProcessingStatus,
    int? PageCount,
    int ChunkCount,
    string? ProcessingError,
    DateTime UploadedAt,
    DateTime? ProcessedAt
);

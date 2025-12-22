using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Query to retrieve PDF file stream for download.
/// Handles authorization check, database lookup, and blob storage retrieval.
/// Returns file stream with metadata for HTTP response.
/// </summary>
/// <param name="PdfId">The PDF document ID to download</param>
/// <param name="UserId">The requesting user ID (for authorization)</param>
/// <param name="IsAdmin">Whether the requesting user is an admin</param>
internal record DownloadPdfQuery(
    Guid PdfId,
    Guid UserId,
    bool IsAdmin
) : IQuery<PdfDownloadResult?>;

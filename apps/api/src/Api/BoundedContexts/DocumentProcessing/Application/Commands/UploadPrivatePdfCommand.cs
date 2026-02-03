using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Command to upload a private PDF associated with a UserLibraryEntry.
/// Issue #3479: Private PDF Upload Endpoint
/// </summary>
/// <param name="UserId">The user who owns the library entry</param>
/// <param name="UserLibraryEntryId">The library entry to associate the PDF with</param>
/// <param name="PdfFile">The PDF file to upload</param>
internal record UploadPrivatePdfCommand(
    Guid UserId,
    Guid UserLibraryEntryId,
    IFormFile PdfFile
) : ICommand<PrivatePdfUploadResult>;

/// <summary>
/// Result of private PDF upload operation.
/// </summary>
/// <param name="PdfId">The ID of the created PDF document</param>
/// <param name="FileName">The sanitized file name</param>
/// <param name="FileSize">File size in bytes</param>
/// <param name="Status">Current processing status</param>
/// <param name="SseStreamUrl">Optional SSE stream URL for progress tracking</param>
internal record PrivatePdfUploadResult(
    Guid PdfId,
    string FileName,
    long FileSize,
    string Status,
    string? SseStreamUrl = null
);

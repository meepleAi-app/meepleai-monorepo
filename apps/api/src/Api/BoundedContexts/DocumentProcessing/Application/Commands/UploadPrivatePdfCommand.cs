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
/// Issue #3653: Enhanced with quota information.
/// </summary>
/// <param name="PdfId">The ID of the created PDF document</param>
/// <param name="FileName">The sanitized file name</param>
/// <param name="FileSize">File size in bytes</param>
/// <param name="Status">Current processing status</param>
/// <param name="SseStreamUrl">SSE stream URL for progress tracking</param>
/// <param name="QuotaRemaining">Remaining quota information</param>
internal record PrivatePdfUploadResult(
    Guid PdfId,
    string FileName,
    long FileSize,
    string Status,
    string SseStreamUrl,
    QuotaRemainingInfo QuotaRemaining
);

/// <summary>
/// Quota remaining information returned with upload response.
/// Issue #3653: Per-game, daily, and weekly quota tracking.
/// </summary>
/// <param name="Daily">Remaining daily uploads</param>
/// <param name="Weekly">Remaining weekly uploads</param>
/// <param name="PerGame">Remaining PDFs for this specific game</param>
internal record QuotaRemainingInfo(
    int Daily,
    int Weekly,
    int PerGame
);

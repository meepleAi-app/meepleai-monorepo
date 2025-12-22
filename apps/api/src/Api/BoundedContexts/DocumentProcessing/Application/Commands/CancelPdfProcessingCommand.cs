using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Command to cancel PDF processing for a document.
/// Handles authorization check and background task cancellation.
/// Idempotent: succeeds even if processing already completed/failed.
/// </summary>
/// <param name="PdfId">The PDF document ID to cancel processing for</param>
/// <param name="UserId">The requesting user ID (for authorization)</param>
/// <param name="IsAdmin">Whether the requesting user is an admin</param>
internal record CancelPdfProcessingCommand(
    Guid PdfId,
    Guid UserId,
    bool IsAdmin
) : ICommand<CancelProcessingResult>;

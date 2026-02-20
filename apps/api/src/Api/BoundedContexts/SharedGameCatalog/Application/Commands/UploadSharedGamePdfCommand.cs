using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to upload a PDF for a shared game, bypassing user library.
/// Admin/Editor only. Creates PdfDocument with SharedGameId and enqueues processing.
/// Issue #4922: Admin upload endpoint for shared game documents.
/// </summary>
internal record UploadSharedGamePdfCommand(
    Guid SharedGameId,
    IFormFile File,
    SharedGameDocumentType DocumentType,
    string Version,
    bool SetAsActive,
    List<string>? Tags,
    Guid AdminUserId
) : ICommand<UploadSharedGamePdfResult>;

/// <summary>
/// Result of admin shared game PDF upload.
/// </summary>
public record UploadSharedGamePdfResult(
    Guid PdfDocumentId,
    Guid SharedGameDocumentId,
    string ProcessingStatus
);

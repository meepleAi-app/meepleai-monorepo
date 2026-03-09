using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Command to reclassify a document's category, base document link, and version label.
/// Issue #5447: Admin can reclassify documents post-upload.
/// </summary>
internal record ReclassifyDocumentCommand(
    Guid PdfId,
    string Category,
    Guid? BaseDocumentId,
    string? VersionLabel) : ICommand<ReclassifyDocumentResult>;

/// <summary>
/// Result of reclassifying a document.
/// </summary>
internal record ReclassifyDocumentResult(bool Success, string Message, Guid? PdfId);

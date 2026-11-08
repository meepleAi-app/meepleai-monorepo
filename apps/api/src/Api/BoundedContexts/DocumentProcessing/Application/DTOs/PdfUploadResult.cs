namespace Api.BoundedContexts.DocumentProcessing.Application.DTOs;

/// <summary>
/// Result of a PDF upload operation.
/// </summary>
/// <remarks>
/// <c>ProcessingJobId</c> is set when the upload was handed to the processing queue (an admin or editor
/// upload, #4443), null when a background task processes it.
/// </remarks>
internal record PdfUploadResult(
    bool Success,
    string Message,
    PdfDocumentDto? Document,
    ExistingKbInfoDto? ExistingKb = null,
    Guid? ProcessingJobId = null);

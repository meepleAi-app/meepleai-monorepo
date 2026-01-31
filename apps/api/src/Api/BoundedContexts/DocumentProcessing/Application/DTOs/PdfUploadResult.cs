namespace Api.BoundedContexts.DocumentProcessing.Application.DTOs;

/// <summary>
/// Result of a PDF upload operation.
/// </summary>
internal record PdfUploadResult(bool Success, string Message, PdfDocumentDto? Document);

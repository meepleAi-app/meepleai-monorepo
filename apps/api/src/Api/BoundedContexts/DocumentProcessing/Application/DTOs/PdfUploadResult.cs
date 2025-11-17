namespace Api.BoundedContexts.DocumentProcessing.Application.DTOs;

/// <summary>
/// Result of a PDF upload operation.
/// </summary>
public record PdfUploadResult(bool Success, string Message, PdfDocumentDto? Document);

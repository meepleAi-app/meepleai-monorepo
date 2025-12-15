namespace Api.BoundedContexts.DocumentProcessing.Application.DTOs;

/// <summary>
/// Result of a PDF deletion operation.
/// </summary>
internal record PdfDeleteResult(bool Success, string Message, string? GameId);

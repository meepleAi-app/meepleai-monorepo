namespace Api.BoundedContexts.DocumentProcessing.Application.DTOs;

/// <summary>
/// Result of a PDF deletion operation.
/// </summary>
public record PdfDeleteResult(bool Success, string Message, string? GameId);

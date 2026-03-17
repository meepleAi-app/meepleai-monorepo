using Api.BoundedContexts.DocumentProcessing.Domain.Enums;

namespace Api.BoundedContexts.DocumentProcessing.Application.DTOs;

/// <summary>
/// Result of a rulebook upload operation.
/// Indicates whether the PDF was newly uploaded or reused from an existing document.
/// </summary>
internal record RulebookUploadResult(
    Guid PdfDocumentId,
    bool IsNew,
    string Status,
    string Message)
{
    /// <summary>
    /// Maps a PdfProcessingState to a user-facing status string.
    /// </summary>
    public static string MapStatus(PdfProcessingState state) => state switch
    {
        PdfProcessingState.Ready => "ready",
        PdfProcessingState.Failed => "failed",
        _ => "pending"
    };
}

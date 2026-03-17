namespace Api.BoundedContexts.DocumentProcessing.Application.DTOs;

internal sealed record RulebookUploadResult(
    Guid PdfDocumentId,
    bool IsNew,
    string Status,
    string Message)
{
    /// <summary>
    /// Maps PdfProcessingState to client-facing status string.
    /// Pending/Uploading → "pending", Extracting..Indexing → "processing", Ready → "ready", Failed → "failed"
    /// </summary>
    public static string MapStatus(Domain.Enums.PdfProcessingState state) => state switch
    {
        Domain.Enums.PdfProcessingState.Pending => "pending",
        Domain.Enums.PdfProcessingState.Uploading => "pending",
        Domain.Enums.PdfProcessingState.Extracting => "processing",
        Domain.Enums.PdfProcessingState.Chunking => "processing",
        Domain.Enums.PdfProcessingState.Embedding => "processing",
        Domain.Enums.PdfProcessingState.Indexing => "processing",
        Domain.Enums.PdfProcessingState.Ready => "ready",
        Domain.Enums.PdfProcessingState.Failed => "failed",
        _ => "unknown"
    };
}

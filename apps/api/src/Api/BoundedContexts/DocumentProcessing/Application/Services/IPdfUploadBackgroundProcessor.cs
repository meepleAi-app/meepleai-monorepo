namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Coordinates the background PDF processing pipeline for the upload flow.
/// Wraps <see cref="IPdfProcessingPipelineService"/> and adds upload-specific concerns:
/// quota confirmation on success and quota release on failure.
/// </summary>
internal interface IPdfUploadBackgroundProcessor
{
    /// <summary>
    /// Processes a PDF document through the full pipeline and handles quota lifecycle.
    /// On success: marks PDF as Ready, publishes PdfStateChangedEvent, confirms quota.
    /// On failure: marks PDF as Failed, releases quota.
    /// </summary>
    /// <param name="pdfDocumentId">The PDF document GUID (as string for compatibility with background task API).</param>
    /// <param name="filePath">Path to the PDF file (used as filesystem fallback when blob storage is unavailable).</param>
    /// <param name="uploadedByUserId">The user who uploaded the PDF.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ProcessAsync(string pdfDocumentId, string filePath, Guid uploadedByUserId, CancellationToken cancellationToken);
}

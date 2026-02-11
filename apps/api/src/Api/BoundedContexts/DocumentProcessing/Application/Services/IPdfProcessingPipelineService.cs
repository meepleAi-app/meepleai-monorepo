namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Shared PDF processing pipeline service.
/// Encapsulates the full extract-chunk-embed-index flow for reuse
/// across upload handlers and the stale PDF recovery job.
/// </summary>
internal interface IPdfProcessingPipelineService
{
    /// <summary>
    /// Processes a PDF document through the full pipeline:
    /// validate &amp; set "processing" → extract text → extract tables →
    /// chunk → batch embed → index in Qdrant → save text chunks → mark "completed".
    /// </summary>
    /// <param name="pdfDocumentId">The PDF document ID.</param>
    /// <param name="filePath">Path to the PDF file on disk.</param>
    /// <param name="uploadedByUserId">The user who uploaded the PDF.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ProcessAsync(Guid pdfDocumentId, string filePath, Guid uploadedByUserId, CancellationToken cancellationToken);
}

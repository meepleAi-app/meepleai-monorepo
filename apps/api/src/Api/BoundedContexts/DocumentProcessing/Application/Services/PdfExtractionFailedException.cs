namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Thrown by <see cref="PdfProcessingPipelineService"/> when text extraction fails, carrying
/// whether the failure is permanent (file will never succeed on retry — e.g. Unstructured 413)
/// so the pipeline's catch-all can classify PdfDocument.ErrorCategory correctly instead of
/// leaving it unset (silently treated as retriable by RetryFailedPdfsJob). Issue #3589.
/// </summary>
internal sealed class PdfExtractionFailedException : InvalidOperationException
{
    public bool IsPermanentFailure { get; }

    public PdfExtractionFailedException(string message, bool isPermanentFailure)
        : base(message)
    {
        IsPermanentFailure = isPermanentFailure;
    }
}

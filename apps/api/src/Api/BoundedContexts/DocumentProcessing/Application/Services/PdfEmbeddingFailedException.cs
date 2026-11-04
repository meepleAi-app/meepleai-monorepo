namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// The embedding step of the queue pipeline failed: the service refused a batch, returned the wrong
/// number of vectors, or returned an invalid one.
/// </summary>
/// <remarks>
/// #4443: <c>PdfProcessingPipelineService</c> threw a plain <see cref="InvalidOperationException"/>
/// here, and the document ended <c>Failed</c> with a NULL error category. The upload's background task
/// records the same failure as <c>ErrorCategory.Service</c> — transient, so <c>RetryFailedPdfsJob</c>
/// retries it. Since ADR-100 the queue pipeline processes every admin upload, so it classifies the same
/// way.
/// </remarks>
internal sealed class PdfEmbeddingFailedException : InvalidOperationException
{
    public PdfEmbeddingFailedException(string message)
        : base(message)
    {
    }

    public PdfEmbeddingFailedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

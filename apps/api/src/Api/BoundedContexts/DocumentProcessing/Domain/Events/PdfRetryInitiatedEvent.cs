using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Events;

/// <summary>
/// Domain event raised when a failed PDF processing is manually retried.
/// Issue #4216: Triggers pipeline resumption and notification.
/// </summary>
internal sealed class PdfRetryInitiatedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the PDF document identifier.
    /// </summary>
    public Guid PdfDocumentId { get; }

    /// <summary>
    /// Gets the retry attempt number (1-based).
    /// </summary>
    public int RetryCount { get; }

    /// <summary>
    /// Gets the user who uploaded the document (for notification routing).
    /// </summary>
    public Guid UploadedByUserId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfRetryInitiatedEvent"/> class.
    /// </summary>
    /// <param name="pdfDocumentId">The PDF document identifier.</param>
    /// <param name="retryCount">The retry attempt number.</param>
    /// <param name="uploadedByUserId">The user ID for notification routing.</param>
    public PdfRetryInitiatedEvent(
        Guid pdfDocumentId,
        int retryCount,
        Guid uploadedByUserId)
    {
        PdfDocumentId = pdfDocumentId;
        RetryCount = retryCount;
        UploadedByUserId = uploadedByUserId;
    }
}

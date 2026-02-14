using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Events;

/// <summary>
/// Domain event raised when PDF processing fails.
/// Issue #4216: Enables error categorization and retry eligibility tracking.
/// </summary>
internal sealed class PdfFailedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the PDF document identifier.
    /// </summary>
    public Guid PdfDocumentId { get; }

    /// <summary>
    /// Gets the error category for targeted retry strategy.
    /// </summary>
    public ErrorCategory ErrorCategory { get; }

    /// <summary>
    /// Gets the state where failure occurred (for resume point).
    /// </summary>
    public PdfProcessingState FailedAtState { get; }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public string ErrorMessage { get; }

    /// <summary>
    /// Gets the user who uploaded the document (for notification routing).
    /// </summary>
    public Guid UploadedByUserId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfFailedEvent"/> class.
    /// </summary>
    /// <param name="pdfDocumentId">The PDF document identifier.</param>
    /// <param name="errorCategory">The error category.</param>
    /// <param name="failedAtState">The state where failure occurred.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="uploadedByUserId">The user ID for notification routing.</param>
    public PdfFailedEvent(
        Guid pdfDocumentId,
        ErrorCategory errorCategory,
        PdfProcessingState failedAtState,
        string errorMessage,
        Guid uploadedByUserId)
    {
        PdfDocumentId = pdfDocumentId;
        ErrorCategory = errorCategory;
        FailedAtState = failedAtState;
        ErrorMessage = errorMessage;
        UploadedByUserId = uploadedByUserId;
    }
}

using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Events;

/// <summary>
/// Domain event raised when a PDF document transitions between processing states.
/// Issue #4215: Enables real-time status updates and notification triggers.
/// </summary>
internal sealed class PdfStateChangedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the PDF document identifier.
    /// </summary>
    public Guid PdfDocumentId { get; }

    /// <summary>
    /// Gets the previous processing state.
    /// </summary>
    public PdfProcessingState PreviousState { get; }

    /// <summary>
    /// Gets the new processing state.
    /// </summary>
    public PdfProcessingState NewState { get; }

    /// <summary>
    /// Gets the user who uploaded the document (for notification routing).
    /// </summary>
    public Guid UploadedByUserId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfStateChangedEvent"/> class.
    /// </summary>
    /// <param name="pdfDocumentId">The PDF document identifier.</param>
    /// <param name="previousState">The previous state.</param>
    /// <param name="newState">The new state.</param>
    /// <param name="uploadedByUserId">The user ID for notification routing.</param>
    public PdfStateChangedEvent(
        Guid pdfDocumentId,
        PdfProcessingState previousState,
        PdfProcessingState newState,
        Guid uploadedByUserId)
    {
        PdfDocumentId = pdfDocumentId;
        PreviousState = previousState;
        NewState = newState;
        UploadedByUserId = uploadedByUserId;
    }
}

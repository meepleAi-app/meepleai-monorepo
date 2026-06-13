using System.Text.Json.Serialization;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Events;

/// <summary>
/// Domain event raised when a PDF document transitions between processing states.
/// Issue #4215: Enables real-time status updates and notification triggers.
/// Issue #2245 (epic #2242 Sub #3): registered in
/// <see cref="Api.Infrastructure.DomainEventLog.EventTypeRegistry"/> with alias
/// <c>"pdf.state.changed"</c> so the admin LiveEventLog SSE stream can render real-time
/// pipeline progress and the FE can drop polling.
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
    /// Mirror of <see cref="PdfDocumentId"/> following the convention required by
    /// <see cref="Api.Infrastructure.DomainEventLog.DomainEventLogMapper"/>: the mapper
    /// reflects on a property named <c>AggregateId</c> to populate
    /// <c>domain_event_logs.aggregate_id</c> without a custom override. Issue #2245.
    /// <c>[JsonIgnore]</c> keeps the SSE payload schema clean
    /// (<c>pdfDocumentId</c> remains the canonical wire-format field).
    /// </summary>
    [JsonIgnore]
    public Guid AggregateId => PdfDocumentId;

    /// <summary>
    /// Mirror of <see cref="UploadedByUserId"/> for the
    /// <see cref="Api.Infrastructure.DomainEventLog.DomainEventLogMapper"/> reflection
    /// extraction of <c>domain_event_logs.user_id</c>. Issue #2245. <c>[JsonIgnore]</c>
    /// keeps the SSE payload schema clean (<c>uploadedByUserId</c> remains canonical).
    /// </summary>
    [JsonIgnore]
    public Guid UserId => UploadedByUserId;

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

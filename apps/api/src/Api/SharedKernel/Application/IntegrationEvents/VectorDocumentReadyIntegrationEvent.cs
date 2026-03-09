using Api.SharedKernel.Domain.Interfaces;

namespace Api.SharedKernel.Application.IntegrationEvents;

/// <summary>
/// Integration event published when a vector document has been indexed and is ready for use.
/// Cross-context consumers:
/// - UserNotifications: creates in-app/email/push notifications
/// - DocumentProcessing: updates PDF processing state to Ready
/// Issue #5237: Decouple VectorDocumentIndexedEventHandler from UserNotifications and Authentication.
/// </summary>
internal sealed record VectorDocumentReadyIntegrationEvent : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string SourceContext => "KnowledgeBase";

    /// <summary>
    /// The ID of the vector document that was indexed.
    /// </summary>
    public required Guid DocumentId { get; init; }

    /// <summary>
    /// The ID of the game associated with the vector document.
    /// </summary>
    public required Guid GameId { get; init; }

    /// <summary>
    /// Number of chunks created during indexing.
    /// </summary>
    public required int ChunkCount { get; init; }

    /// <summary>
    /// The ID of the source PDF document.
    /// </summary>
    public required Guid PdfDocumentId { get; init; }

    /// <summary>
    /// The user who uploaded the PDF.
    /// </summary>
    public required Guid UploadedByUserId { get; init; }

    /// <summary>
    /// The original file name of the uploaded PDF.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// The current processing state of the PDF document (before this event).
    /// </summary>
    public required string CurrentProcessingState { get; init; }
}

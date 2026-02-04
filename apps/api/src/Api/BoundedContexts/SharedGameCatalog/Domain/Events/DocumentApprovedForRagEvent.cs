using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Event raised when a document is approved for RAG processing.
/// </summary>
public record DocumentApprovedForRagEvent(
    Guid DocumentId,
    Guid SharedGameId,
    Guid PdfDocumentId,
    Guid ApprovedBy,
    DateTime ApprovedAt) : INotification;

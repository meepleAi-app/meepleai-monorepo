using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Event raised when a document is added to a shared game.
/// </summary>
public record SharedGameDocumentAddedEvent(
    Guid SharedGameId,
    Guid DocumentId,
    Guid PdfDocumentId,
    SharedGameDocumentType DocumentType,
    string Version) : INotification;

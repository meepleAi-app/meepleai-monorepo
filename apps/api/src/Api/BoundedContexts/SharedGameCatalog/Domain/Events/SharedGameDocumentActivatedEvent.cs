using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Event raised when a document version is set as active.
/// </summary>
public record SharedGameDocumentActivatedEvent(
    Guid SharedGameId,
    Guid DocumentId,
    SharedGameDocumentType DocumentType,
    string Version) : INotification;

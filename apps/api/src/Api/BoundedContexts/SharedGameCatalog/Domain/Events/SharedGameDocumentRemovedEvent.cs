using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Event raised when a document is removed from a shared game.
/// </summary>
public record SharedGameDocumentRemovedEvent(
    Guid SharedGameId,
    Guid DocumentId,
    SharedGameDocumentType DocumentType) : INotification;

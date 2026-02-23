using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using MediatR;

namespace Api.BoundedContexts.EntityRelationships.Domain.Events;

/// <summary>Domain event raised when a new EntityLink is created.</summary>
public sealed record EntityLinkCreatedEvent(
    Guid EntityLinkId,
    MeepleEntityType SourceType,
    Guid SourceId,
    MeepleEntityType TargetType,
    Guid TargetId,
    EntityLinkType LinkType,
    Guid? CreatedByUserId) : INotification;

/// <summary>Domain event raised when an EntityLink is deleted.</summary>
public sealed record EntityLinkDeletedEvent(
    Guid EntityLinkId,
    Guid? DeletedByUserId) : INotification;

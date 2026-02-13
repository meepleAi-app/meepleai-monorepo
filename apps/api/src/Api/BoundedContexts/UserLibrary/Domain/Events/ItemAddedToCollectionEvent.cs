using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.UserLibrary.Domain.Events;

/// <summary>
/// Domain event raised when an item is added to a user's collection.
/// Issue #4263: Phase 2 - Generic UserCollection System
/// </summary>
internal sealed class ItemAddedToCollectionEvent : DomainEventBase
{
    /// <summary>
    /// The ID of the collection entry.
    /// </summary>
    public Guid EntryId { get; }

    /// <summary>
    /// The ID of the user who owns the collection.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// The type of entity added to the collection.
    /// </summary>
    public EntityType EntityType { get; }

    /// <summary>
    /// The ID of the entity added to the collection.
    /// </summary>
    public Guid EntityId { get; }

    public ItemAddedToCollectionEvent(Guid entryId, Guid userId, EntityType entityType, Guid entityId)
    {
        EntryId = entryId;
        UserId = userId;
        EntityType = entityType;
        EntityId = entityId;
    }
}

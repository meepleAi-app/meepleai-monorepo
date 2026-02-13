using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.BoundedContexts.UserLibrary.Domain.Events;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.UserLibrary.Domain.Entities;

/// <summary>
/// UserCollectionEntry aggregate root representing a generic entity in a user's collection.
/// Supports any entity type (Player, Event, Session, Agent, Document, ChatSession) with polymorphic references.
/// Issue #4263: Phase 2 - Generic UserCollection System
/// </summary>
internal sealed class UserCollectionEntry : AggregateRoot<Guid>
{
    /// <summary>
    /// The ID of the user who owns this collection entry.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// The type of entity in the collection.
    /// </summary>
    public EntityType EntityType { get; private set; }

    /// <summary>
    /// The ID of the entity (polymorphic reference, validated in domain layer).
    /// </summary>
    public Guid EntityId { get; private set; }

    /// <summary>
    /// Whether this item is marked as a favorite.
    /// </summary>
    public bool IsFavorite { get; private set; }

    /// <summary>
    /// Optional personal notes about the entity.
    /// </summary>
    public string? Notes { get; private set; }

    /// <summary>
    /// Flexible metadata for entity-specific data.
    /// </summary>
    public CollectionMetadata? Metadata { get; private set; }

    /// <summary>
    /// When the entity was added to the collection.
    /// </summary>
    public DateTime AddedAt { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private UserCollectionEntry() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new collection entry for a user's entity.
    /// </summary>
    /// <param name="id">Unique identifier for the entry</param>
    /// <param name="userId">The user who owns this entry</param>
    /// <param name="entityType">The type of entity being collected</param>
    /// <param name="entityId">The ID of the entity being collected</param>
    public UserCollectionEntry(Guid id, Guid userId, EntityType entityType, Guid entityId) : base(id)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));
        if (entityId == Guid.Empty)
            throw new ArgumentException("EntityId cannot be empty", nameof(entityId));
        if (!Enum.IsDefined(entityType))
            throw new ArgumentException("Invalid EntityType", nameof(entityType));

        UserId = userId;
        EntityType = entityType;
        EntityId = entityId;
        AddedAt = DateTime.UtcNow;
        IsFavorite = false;
        Metadata = CollectionMetadata.Empty();

        AddDomainEvent(new ItemAddedToCollectionEvent(id, userId, entityType, entityId));
    }

    /// <summary>
    /// Updates the personal notes for this collection item.
    /// </summary>
    /// <param name="notes">New notes (or null to clear)</param>
    public void UpdateNotes(string? notes)
    {
        Notes = notes;
    }

    /// <summary>
    /// Toggles the favorite status.
    /// </summary>
    public void ToggleFavorite()
    {
        IsFavorite = !IsFavorite;
    }

    /// <summary>
    /// Marks this item as a favorite.
    /// </summary>
    public void MarkAsFavorite()
    {
        IsFavorite = true;
    }

    /// <summary>
    /// Removes the favorite mark from this item.
    /// </summary>
    public void RemoveFavorite()
    {
        IsFavorite = false;
    }

    /// <summary>
    /// Sets the favorite status explicitly.
    /// </summary>
    /// <param name="isFavorite">True to mark as favorite, false otherwise</param>
    public void SetFavorite(bool isFavorite)
    {
        IsFavorite = isFavorite;
    }

    /// <summary>
    /// Updates the flexible metadata for entity-specific data.
    /// </summary>
    /// <param name="metadata">The metadata to set</param>
    public void UpdateMetadata(CollectionMetadata metadata)
    {
        Metadata = metadata ?? CollectionMetadata.Empty();
    }

    /// <summary>
    /// Prepares this entry for removal, raising the appropriate domain event.
    /// Call this before deleting the entry from the repository.
    /// </summary>
    public void PrepareForRemoval()
    {
        AddDomainEvent(new ItemRemovedFromCollectionEvent(Id, UserId, EntityType, EntityId));
    }
}

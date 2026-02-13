namespace Api.Infrastructure.Entities.UserLibrary;

/// <summary>
/// UserCollectionEntry persistence entity.
/// Represents a generic entity in a user's collection.
/// Issue #4263: Phase 2 - Generic UserCollection System
/// </summary>
public class UserCollectionEntryEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Reference to the user who owns this collection entry.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Type of entity being collected (Player, Event, Session, Agent, Document, ChatSession).
    /// Stored as string for database readability.
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Polymorphic reference to the entity being collected.
    /// </summary>
    public Guid EntityId { get; set; }

    /// <summary>
    /// Whether this item is marked as a favorite.
    /// </summary>
    public bool IsFavorite { get; set; }

    /// <summary>
    /// Optional personal notes about the entity (max 500 characters).
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Flexible metadata stored as JSON for entity-specific data.
    /// </summary>
    public string? MetadataJson { get; set; }

    /// <summary>
    /// When the entity was added to the collection.
    /// </summary>
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the entry was created (audit trail).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the entry was last updated (null if never updated).
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public UserEntity? User { get; set; }
}

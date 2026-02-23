using Api.BoundedContexts.EntityRelationships.Domain.Constants;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.EntityRelationships.Domain.Events;

namespace Api.BoundedContexts.EntityRelationships.Domain.Aggregates;

/// <summary>
/// Aggregate root representing a directed or bilateral relationship between two MeepleAI entities.
///
/// Business Rules:
/// - BR-04: scope=User → IsAdminApproved = true automatically
/// - BR-05: scope=Shared → requires Admin role (enforced at endpoint level)
/// - BR-08: Unique per (sourceEntityType, sourceEntityId, targetEntityType, targetEntityId, linkType)
/// - IsBidirectional is derived from LinkType (cannot be set manually)
/// - Source entity must differ from target entity (same type + same id not allowed)
/// </summary>
public sealed class EntityLink
{
    private readonly List<object> _domainEvents = [];

    public Guid Id { get; private set; }

    /// <summary>Type of the source entity (e.g. Game, Agent).</summary>
    public MeepleEntityType SourceEntityType { get; private set; }
    public Guid SourceEntityId { get; private set; }

    /// <summary>Type of the target entity.</summary>
    public MeepleEntityType TargetEntityType { get; private set; }
    public Guid TargetEntityId { get; private set; }

    public EntityLinkType LinkType { get; private set; }

    /// <summary>
    /// True for bilateral link types (CompanionTo, RelatedTo, CollaboratesWith).
    /// Derived from LinkType — not settable externally.
    /// </summary>
    public bool IsBidirectional { get; private set; }

    /// <summary>"User" | "Shared" — user-scope links are auto-approved (BR-04).</summary>
    public EntityLinkScope Scope { get; private set; }

    /// <summary>The user who created/owns this link.</summary>
    public Guid OwnerUserId { get; private set; }

    /// <summary>Optional JSONB metadata (notes, bgg_id, ordering hints).</summary>
    public string? Metadata { get; private set; }

    public bool IsAdminApproved { get; private set; }
    public bool IsBggImported { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // EF Core constructor
    private EntityLink() { }

    /// <summary>
    /// Factory method for creating a new EntityLink.
    /// Validates source != target, derives IsBidirectional, enforces BR-04.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when source and target are the same entity.</exception>
    public static EntityLink Create(
        MeepleEntityType sourceEntityType,
        Guid sourceEntityId,
        MeepleEntityType targetEntityType,
        Guid targetEntityId,
        EntityLinkType linkType,
        EntityLinkScope scope,
        Guid ownerUserId,
        string? metadata = null,
        bool isBggImported = false)
    {
        // Validate: source != target (same type AND same id = self-reference)
        if (sourceEntityType == targetEntityType && sourceEntityId == targetEntityId)
            throw new ArgumentException("Source and target entity cannot be the same.", nameof(targetEntityId));

        var truncatedMetadata = metadata?.Length > EntityRelationshipsConstants.MetadataMaxLength
            ? metadata[..EntityRelationshipsConstants.MetadataMaxLength]
            : metadata;

        var link = new EntityLink
        {
            Id = Guid.NewGuid(),
            SourceEntityType = sourceEntityType,
            SourceEntityId = sourceEntityId,
            TargetEntityType = targetEntityType,
            TargetEntityId = targetEntityId,
            LinkType = linkType,
            IsBidirectional = linkType.IsBidirectional(),
            Scope = scope,
            OwnerUserId = ownerUserId,
            Metadata = truncatedMetadata,
            // BR-04: user-scope links are auto-approved; BGG imports are always approved
            IsAdminApproved = scope == EntityLinkScope.User || isBggImported,
            IsBggImported = isBggImported,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        link._domainEvents.Add(new EntityLinkCreatedEvent(
            link.Id, sourceEntityType, sourceEntityId, targetEntityType, targetEntityId, linkType, ownerUserId));

        return link;
    }

    /// <summary>Marks this link as admin-approved (for Shared scope links, BR-05).</summary>
    public void Approve()
    {
        IsAdminApproved = true;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Updates optional metadata (notes, bgg_id, ordering).</summary>
    public void UpdateMetadata(string? metadata)
    {
        Metadata = metadata?.Length > EntityRelationshipsConstants.MetadataMaxLength
            ? metadata[..EntityRelationshipsConstants.MetadataMaxLength]
            : metadata;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Soft-deletes the link and records a deletion domain event.</summary>
    public void Delete(Guid deletedByUserId)
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        _domainEvents.Add(new EntityLinkDeletedEvent(Id, deletedByUserId));
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Returns and clears pending domain events.</summary>
    public IReadOnlyList<object> PopDomainEvents()
    {
        var events = _domainEvents.ToList();
        _domainEvents.Clear();
        return events;
    }
}

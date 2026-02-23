using Api.BoundedContexts.EntityRelationships.Domain.Constants;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.EntityRelationships.Domain.Events;
using Api.BoundedContexts.EntityRelationships.Domain.Exceptions;

namespace Api.BoundedContexts.EntityRelationships.Domain.Aggregates;

/// <summary>
/// Aggregate root representing a directed or bilateral relationship between two MeepleAI entities.
///
/// Business Rules:
/// - BR-04: scope=User → IsAdminApproved = true automatically
/// - BR-05: scope=Shared → requires Admin role
/// - BR-08: Unique per (sourceType, sourceId, targetType, targetId, linkType)
/// </summary>
public sealed class EntityLink
{
    private readonly List<object> _domainEvents = [];

    public Guid Id { get; private set; }
    public MeepleEntityType SourceType { get; private set; }
    public Guid SourceId { get; private set; }
    public MeepleEntityType TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public EntityLinkType LinkType { get; private set; }
    public EntityLinkScope Scope { get; private set; }
    public bool IsAdminApproved { get; private set; }
    public bool IsBggImported { get; private set; }
    public string? Notes { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // EF Core constructor
    private EntityLink() { }

    /// <summary>
    /// Factory method for creating a new EntityLink.
    /// Enforces BR-04 (auto-approve user-scope links).
    /// </summary>
    public static EntityLink Create(
        MeepleEntityType sourceType,
        Guid sourceId,
        MeepleEntityType targetType,
        Guid targetId,
        EntityLinkType linkType,
        EntityLinkScope scope,
        Guid? createdByUserId,
        string? notes = null,
        bool isBggImported = false)
    {
        var link = new EntityLink
        {
            Id = Guid.NewGuid(),
            SourceType = sourceType,
            SourceId = sourceId,
            TargetType = targetType,
            TargetId = targetId,
            LinkType = linkType,
            Scope = scope,
            // BR-04: user-scope links are auto-approved; BGG imports are always approved
            IsAdminApproved = scope == EntityLinkScope.User || isBggImported,
            IsBggImported = isBggImported,
            Notes = notes?.Length > EntityRelationshipsConstants.NotesMaxLength
                ? notes[..EntityRelationshipsConstants.NotesMaxLength]
                : notes,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        link._domainEvents.Add(new EntityLinkCreatedEvent(
            link.Id, sourceType, sourceId, targetType, targetId, linkType, createdByUserId));

        return link;
    }

    /// <summary>Marks this link as admin-approved (for Shared scope links).</summary>
    public void Approve()
    {
        IsAdminApproved = true;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Records deletion domain event before removal.</summary>
    public void Delete(Guid? deletedByUserId)
    {
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

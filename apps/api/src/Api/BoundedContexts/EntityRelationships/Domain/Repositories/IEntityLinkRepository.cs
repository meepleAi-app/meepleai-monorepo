using Api.BoundedContexts.EntityRelationships.Domain.Aggregates;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;

namespace Api.BoundedContexts.EntityRelationships.Domain.Repositories;

/// <summary>
/// Repository interface for EntityLink aggregate.
/// </summary>
public interface IEntityLinkRepository
{
    /// <summary>Gets a single EntityLink by its identifier.</summary>
    Task<EntityLink?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all links where the given entity is source or (for bilateral links) target.
    /// </summary>
    Task<IReadOnlyList<EntityLink>> GetBySourceAsync(
        MeepleEntityType sourceEntityType,
        Guid sourceEntityId,
        Guid? ownerUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the total count of links for a given source entity.</summary>
    Task<int> GetCountBySourceAsync(
        MeepleEntityType sourceEntityType,
        Guid sourceEntityId,
        Guid? ownerUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Checks whether an identical link already exists (BR-08).</summary>
    Task<bool> ExistsAsync(
        MeepleEntityType sourceEntityType,
        Guid sourceEntityId,
        MeepleEntityType targetEntityType,
        Guid targetEntityId,
        EntityLinkType linkType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the total count of links where the entity appears as source
    /// OR as target of a bidirectional link.
    /// Optimised: COUNT(*) only, no data loading (Issue #5136).
    /// </summary>
    Task<int> GetCountForEntityAsync(
        MeepleEntityType entityType,
        Guid entityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all links where the given entity appears as source,
    /// OR as target in a bidirectional link (IsBidirectional=true).
    /// Supports optional filtering by Scope and LinkType (Issue #5135).
    /// </summary>
    Task<IReadOnlyList<EntityLink>> GetForEntityAsync(
        MeepleEntityType entityType,
        Guid entityId,
        EntityLinkScope? scope = null,
        EntityLinkType? linkType = null,
        CancellationToken cancellationToken = default);

    /// <summary>Adds a new EntityLink to the repository.</summary>
    Task AddAsync(EntityLink entityLink, CancellationToken cancellationToken = default);

    /// <summary>Removes an EntityLink from the repository.</summary>
    void Remove(EntityLink entityLink);
}

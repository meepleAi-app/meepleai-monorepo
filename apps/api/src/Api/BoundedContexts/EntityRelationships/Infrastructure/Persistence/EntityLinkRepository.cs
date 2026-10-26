using Api.BoundedContexts.EntityRelationships.Domain.Aggregates;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.EntityRelationships.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.EntityRelationships.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of IEntityLinkRepository (Issue #5132).
/// Uses the direct domain-entity mapping — no persistence entity.
/// </summary>
internal sealed class EntityLinkRepository : RepositoryBase, IEntityLinkRepository
{

    public EntityLinkRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    /// <summary>
    /// Il collegamento, <b>tracciato</b>.
    /// </summary>
    /// <remarks>
    /// #3858 — il DbContext ha come default <c>QueryTrackingBehavior.NoTracking</c> (PERF-06).
    /// Senza <c>AsTracking()</c> l'entita' restituita qui non e' tracciata: il
    /// <c>link.Delete()</c> dell'handler mutava un oggetto scollegato, <c>SaveChangesAsync</c> non
    /// trovava nulla da scrivere e l'endpoint rispondeva <b>204 senza cancellare</b>. Nessun
    /// errore, in nessun log — il fallimento aveva la forma esatta del successo.
    ///
    /// Il tracciamento e' preferibile a un <c>Update()</c> esplicito su entita' scollegata: quella
    /// strada rompe la concorrenza ottimistica sulle entita' che usano <c>xmin</c>, perche' il
    /// valore originale parte da 0 e la <c>WHERE</c> non combacia mai.
    ///
    /// L'unico chiamante e' il comando di cancellazione, quindi il costo del tracciamento non
    /// ricade su alcun percorso di sola lettura.
    /// </remarks>
    public async Task<EntityLink?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.EntityLinks
            .AsTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<EntityLink>> GetBySourceAsync(
        MeepleEntityType sourceEntityType,
        Guid sourceEntityId,
        Guid? ownerUserId = null,
        CancellationToken cancellationToken = default)
    {
        var query = DbContext.EntityLinks
            .Where(x => x.SourceEntityType == sourceEntityType && x.SourceEntityId == sourceEntityId);

        if (ownerUserId.HasValue)
            query = query.Where(x => x.OwnerUserId == ownerUserId.Value);

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> GetCountBySourceAsync(
        MeepleEntityType sourceEntityType,
        Guid sourceEntityId,
        Guid? ownerUserId = null,
        CancellationToken cancellationToken = default)
    {
        var query = DbContext.EntityLinks
            .Where(x => x.SourceEntityType == sourceEntityType && x.SourceEntityId == sourceEntityId);

        if (ownerUserId.HasValue)
            query = query.Where(x => x.OwnerUserId == ownerUserId.Value);

        return await query
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(
        MeepleEntityType sourceEntityType,
        Guid sourceEntityId,
        MeepleEntityType targetEntityType,
        Guid targetEntityId,
        EntityLinkType linkType,
        CancellationToken cancellationToken = default)
    {
        return await DbContext.EntityLinks
            .AnyAsync(x =>
                x.SourceEntityType == sourceEntityType &&
                x.SourceEntityId == sourceEntityId &&
                x.TargetEntityType == targetEntityType &&
                x.TargetEntityId == targetEntityId &&
                x.LinkType == linkType,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> GetCountForEntityAsync(
        MeepleEntityType entityType,
        Guid entityId,
        CancellationToken cancellationToken = default)
    {
        return await LinksOfEntity(entityType, entityId)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> GetCountForEntityVisibleToAsync(
        Guid viewerUserId,
        MeepleEntityType entityType,
        Guid entityId,
        CancellationToken cancellationToken = default)
    {
        return await VisibleTo(LinksOfEntity(entityType, entityId), viewerUserId)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>The links where the entity is the source, or the target of a bidirectional link.</summary>
    private IQueryable<EntityLink> LinksOfEntity(MeepleEntityType entityType, Guid entityId) =>
        DbContext.EntityLinks.Where(x =>
            (x.SourceEntityType == entityType && x.SourceEntityId == entityId) ||
            (x.IsBidirectional && x.TargetEntityType == entityType && x.TargetEntityId == entityId));

    /// <summary>A <c>User</c>-scope link is personal: only its owner sees it. Any other scope is for everybody (#4637).</summary>
    private static IQueryable<EntityLink> VisibleTo(IQueryable<EntityLink> links, Guid viewerUserId) =>
        links.Where(x => x.Scope != EntityLinkScope.User || x.OwnerUserId == viewerUserId);

    public async Task<IReadOnlyList<EntityLink>> GetForEntityAsync(
        MeepleEntityType entityType,
        Guid entityId,
        EntityLinkScope? scope = null,
        EntityLinkType? linkType = null,
        MeepleEntityType? targetEntityType = null,
        CancellationToken cancellationToken = default)
    {
        return await FilteredLinks(LinksOfEntity(entityType, entityId), entityType, scope, linkType, targetEntityType)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<EntityLink>> GetForEntityVisibleToAsync(
        Guid viewerUserId,
        MeepleEntityType entityType,
        Guid entityId,
        EntityLinkScope? scope = null,
        EntityLinkType? linkType = null,
        MeepleEntityType? targetEntityType = null,
        CancellationToken cancellationToken = default)
    {
        return await FilteredLinks(
                VisibleTo(LinksOfEntity(entityType, entityId), viewerUserId),
                entityType, scope, linkType, targetEntityType)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static IQueryable<EntityLink> FilteredLinks(
        IQueryable<EntityLink> query,
        MeepleEntityType entityType,
        EntityLinkScope? scope,
        EntityLinkType? linkType,
        MeepleEntityType? targetEntityType)
    {
        if (scope.HasValue)
            query = query.Where(x => x.Scope == scope.Value);

        if (linkType.HasValue)
            query = query.Where(x => x.LinkType == linkType.Value);

        // Issue #5188: Filter by "other entity" type — direction-aware for bidirectional links.
        // When entity is the source, the other entity is the target (x.TargetEntityType).
        // When entity is the bidirectional target, the other entity is the source (x.SourceEntityType).
        if (targetEntityType.HasValue)
            query = query.Where(x =>
                (x.SourceEntityType == entityType && x.TargetEntityType == targetEntityType.Value) ||
                (x.IsBidirectional && x.TargetEntityType == entityType && x.SourceEntityType == targetEntityType.Value));

        return query.OrderByDescending(x => x.CreatedAt);
    }

    public async Task AddAsync(EntityLink entityLink, CancellationToken cancellationToken = default)
    {
        await DbContext.EntityLinks
            .AddAsync(entityLink, cancellationToken)
            .ConfigureAwait(false);
    }

    public void Remove(EntityLink entityLink)
    {
        DbContext.EntityLinks.Remove(entityLink);
    }
}

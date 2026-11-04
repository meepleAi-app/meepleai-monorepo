using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.Authentication;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Api.BoundedContexts.Authentication.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for ShareLink aggregate.
/// Handles mapping between domain and infrastructure entities.
/// </summary>
internal sealed class ShareLinkRepository : RepositoryBase, IShareLinkRepository
{
    public ShareLinkRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector) { }

    public async Task<ShareLink?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.ShareLinks.FindAsync(new object[] { id }, cancellationToken).ConfigureAwait(false);
        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<IReadOnlyList<ShareLink>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.ShareLinks.Take(10000).ToListAsync(cancellationToken).ConfigureAwait(false);
        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<ShareLink>> FindByThreadIdAsync(Guid threadId, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.ShareLinks
            .Where(sl => sl.ThreadId == threadId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<ShareLink>> FindByCreatorIdAsync(Guid creatorId, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.ShareLinks
            .Where(sl => sl.CreatorId == creatorId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return entities.Select(MapToDomain).ToList();
    }

    public async Task<ShareLink?> FindByThreadIdAndCreatorIdAsync(Guid threadId, Guid creatorId, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.ShareLinks
            .FirstOrDefaultAsync(
                sl => sl.ThreadId == threadId && sl.CreatorId == creatorId,
                cancellationToken).ConfigureAwait(false);
        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<IReadOnlyList<ShareLink>> FindActiveByThreadIdAsync(Guid threadId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var entities = await DbContext.ShareLinks
            .Where(sl => sl.ThreadId == threadId
                      && sl.RevokedAt == null
                      && sl.ExpiresAt > now)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return entities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(ShareLink entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        CollectDomainEvents(entity);
        var infrastructureEntity = MapToInfrastructure(entity);
        await DbContext.ShareLinks.AddAsync(infrastructureEntity, cancellationToken).ConfigureAwait(false);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Salva le modifiche del proprietario: revoca, scadenza, etichetta.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Prima di #4573 copiava sulla riga riletta ogni campo dell'aggregato, contatori compresi, e
    /// <c>RevokedAt</c> come <c>entity.IsRevoked ? entity.RevokedAt : null</c>: cioè il valore letto
    /// dall'handler, anche se nel frattempo un'altra richiesta l'aveva cambiato. Una visita che incrocia una
    /// revoca riportava <c>revoked_at</c> a NULL, e due richieste che si incrociavano perdevano un
    /// incremento di <c>access_count</c>. Due scelte lo impediscono.
    /// </para>
    /// <list type="bullet">
    ///   <item><c>access_count</c> e <c>last_accessed_at</c> non vengono scritti: sono della visita
    ///     (<see cref="TryRecordAccessAsync"/>). Una revoca concorrente a una visita non riporta indietro il
    ///     contatore.</item>
    ///   <item>La scrittura vale solo se nel database il link non è revocato (<c>revoked_at</c> è un token di
    ///     concorrenza, e il suo valore originale è <c>NULL</c>). Ogni modifica dell'aggregato richiede un
    ///     link non revocato (<see cref="ShareLink.Revoke"/>, <see cref="ShareLink.UpdateExpiration"/>),
    ///     quindi è il valore che la richiesta ha letto. Se il link è stato revocato nel frattempo, l'UPDATE
    ///     tocca 0 righe e <c>SaveChangesAsync</c> lancia <see cref="DbUpdateConcurrencyException"/>: una
    ///     revoca non si annulla mai.</item>
    /// </list>
    /// <para>
    /// Non è <c>xmin</c> (ADR-060): ogni visita è un UPDATE e cambia <c>xmin</c>, quindi ogni visita arrivata
    /// durante un comando del proprietario lo farebbe fallire. Un link diffuso e visitato di continuo
    /// diventerebbe difficile da revocare, proprio quando serve.
    /// </para>
    /// </remarks>
    public async Task UpdateAsync(ShareLink entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        CollectDomainEvents(entity);
        // #3882: .AsTracking() richiesto — il default del DbContext e' NoTracking (PERF-06),
        // quindi senza di esso questa lettura e' DETACHED: le mutazioni sotto non raggiungono
        // il change tracker e SaveChangesAsync non scrive, e non solleva.
        var existingEntity = await DbContext.ShareLinks
            .AsTracking()
            .FirstOrDefaultAsync(e => e.Id == entity.Id, cancellationToken)
            .ConfigureAwait(false);
        if (existingEntity == null)
        {
            throw new InvalidOperationException($"ShareLink {entity.Id} not found");
        }

        // Update mutable fields. AccessCount e LastAccessedAt no: vedi le remarks.
        existingEntity.RevokedAt = entity.RevokedAt;
        existingEntity.ExpiresAt = entity.ExpiresAt;
        existingEntity.Label = entity.Label;

        // L'originale è NULL qualunque cosa la riletta abbia trovato: la scrittura dell'aggregato presuppone un
        // link non revocato, e se nel database non lo è più l'UPDATE tocca 0 righe.
        DbContext.Entry(existingEntity).Property(e => e.RevokedAt).OriginalValue = null;

        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> TryRecordAccessAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var counted = await DbContext.ShareLinks
            .Where(e => e.Id == id)
            .Where(ActiveAt(now))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(e => e.AccessCount, e => e.AccessCount + 1)
                    .SetProperty(e => e.LastAccessedAt, now),
                cancellationToken)
            .ConfigureAwait(false);

        return counted == 1;
    }

    public async Task<bool> IsActiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        return await DbContext.ShareLinks
            .Where(e => e.Id == id)
            .Where(ActiveAt(now))
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// La validità di un link sulla riga, in un solo punto: <see cref="TryRecordAccessAsync"/> e
    /// <see cref="IsActiveAsync"/> la usano entrambe, quindi la visita e il commento non possono divergere (#4592).
    /// </summary>
    /// <remarks>
    /// È quella dell'aggregato (<see cref="ShareLink.IsValid"/>): non revocato, e non scaduto
    /// (<c>IsExpired</c> è <c>now &gt; ExpiresAt</c>, quindi un link vale fino all'istante di scadenza compreso).
    /// </remarks>
    private static Expression<Func<ShareLinkEntity, bool>> ActiveAt(DateTime now) =>
        e => e.RevokedAt == null && e.ExpiresAt >= now;

    public async Task DeleteAsync(ShareLink entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var existingEntity = await DbContext.ShareLinks.FindAsync(new object[] { entity.Id }, cancellationToken).ConfigureAwait(false);
        if (existingEntity != null)
        {
            DbContext.ShareLinks.Remove(existingEntity);
            await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.ShareLinks.AnyAsync(sl => sl.Id == id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Maps infrastructure entity to domain aggregate.
    /// </summary>
    private static ShareLink MapToDomain(ShareLinkEntity entity)
    {
        // Use reflection to reconstruct domain aggregate from infrastructure state
        var shareLink = (ShareLink)Activator.CreateInstance(typeof(ShareLink), true)!;

        typeof(ShareLink).GetProperty(nameof(ShareLink.Id))!.SetValue(shareLink, entity.Id);
        typeof(ShareLink).GetProperty(nameof(ShareLink.ThreadId))!.SetValue(shareLink, entity.ThreadId);
        typeof(ShareLink).GetProperty(nameof(ShareLink.CreatorId))!.SetValue(shareLink, entity.CreatorId);
        typeof(ShareLink).GetProperty(nameof(ShareLink.Role))!.SetValue(shareLink, entity.Role);
        typeof(ShareLink).GetProperty(nameof(ShareLink.ExpiresAt))!.SetValue(shareLink, entity.ExpiresAt);
        typeof(ShareLink).GetProperty(nameof(ShareLink.CreatedAt))!.SetValue(shareLink, entity.CreatedAt);
        typeof(ShareLink).GetProperty(nameof(ShareLink.RevokedAt))!.SetValue(shareLink, entity.RevokedAt);
        typeof(ShareLink).GetProperty(nameof(ShareLink.Label))!.SetValue(shareLink, entity.Label);
        typeof(ShareLink).GetProperty(nameof(ShareLink.AccessCount))!.SetValue(shareLink, entity.AccessCount);
        typeof(ShareLink).GetProperty(nameof(ShareLink.LastAccessedAt))!.SetValue(shareLink, entity.LastAccessedAt);

        return shareLink;
    }

    /// <summary>
    /// Maps domain aggregate to infrastructure entity.
    /// </summary>
    private static ShareLinkEntity MapToInfrastructure(ShareLink domain)
    {
        return new ShareLinkEntity
        {
            Id = domain.Id,
            ThreadId = domain.ThreadId,
            CreatorId = domain.CreatorId,
            Role = domain.Role,
            ExpiresAt = domain.ExpiresAt,
            CreatedAt = domain.CreatedAt,
            RevokedAt = domain.RevokedAt,
            Label = domain.Label,
            AccessCount = domain.AccessCount,
            LastAccessedAt = domain.LastAccessedAt
        };
    }
}

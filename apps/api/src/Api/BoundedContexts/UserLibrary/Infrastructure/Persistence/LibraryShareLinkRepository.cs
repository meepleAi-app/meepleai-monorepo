using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserLibrary;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserLibrary.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of LibraryShareLink repository.
/// Maps between domain LibraryShareLink entity and LibraryShareLinkEntity persistence model.
/// </summary>
internal class LibraryShareLinkRepository : RepositoryBase, ILibraryShareLinkRepository
{
    public LibraryShareLinkRepository(
        MeepleAiDbContext dbContext,
        IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<LibraryShareLink?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.LibraryShareLinks
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<LibraryShareLink>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.LibraryShareLinks
            .AsNoTracking()
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<LibraryShareLink?> GetByShareTokenAsync(string shareToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(shareToken))
            return null;

        var entity = await DbContext.LibraryShareLinks
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.ShareToken == shareToken, cancellationToken)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<LibraryShareLink?> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var entity = await DbContext.LibraryShareLinks
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .Where(e => e.RevokedAt == null)
            .Where(e => e.ExpiresAt == null || e.ExpiresAt > now)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<LibraryShareLink>> GetAllByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.LibraryShareLinks
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<int> CountRecentByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddHours(-24);
        return await DbContext.LibraryShareLinks
            .AsNoTracking()
            .CountAsync(e => e.UserId == userId && e.CreatedAt >= cutoff, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(LibraryShareLink entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        CollectDomainEvents(entity);

        var persistenceEntity = MapToPersistence(entity);
        await DbContext.LibraryShareLinks.AddAsync(persistenceEntity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(LibraryShareLink entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        CollectDomainEvents(entity);

        var persistenceEntity = MapToPersistence(entity);
        DbContext.LibraryShareLinks.Update(persistenceEntity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(LibraryShareLink entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        CollectDomainEvents(entity);

        var persistenceEntity = MapToPersistence(entity);
        DbContext.LibraryShareLinks.Remove(persistenceEntity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.LibraryShareLinks
            .AsNoTracking()
            .AnyAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Maps persistence entity to domain entity.
    /// </summary>
    /// <remarks>
    /// Passa da <see cref="LibraryShareLink.Reconstitute"/>, non dalla factory di creazione: quella rifiuta
    /// una scadenza passata, e un link scaduto diventava illeggibile (#4544). Ogni colonna scritta da
    /// <see cref="MapToPersistence"/> torna qui: una colonna mancante in una delle due direzioni verrebbe
    /// azzerata al primo <see cref="UpdateAsync"/> (vedi <c>LibraryShareLinkRepositoryTests</c>).
    /// </remarks>
    private static LibraryShareLink MapToDomain(LibraryShareLinkEntity entity) =>
        LibraryShareLink.Reconstitute(
            id: entity.Id,
            userId: entity.UserId,
            shareToken: entity.ShareToken,
            privacyLevel: (LibrarySharePrivacyLevel)entity.PrivacyLevel,
            includeNotes: entity.IncludeNotes,
            createdAt: entity.CreatedAt,
            expiresAt: entity.ExpiresAt,
            revokedAt: entity.RevokedAt,
            viewCount: entity.ViewCount,
            lastAccessedAt: entity.LastAccessedAt);

    /// <summary>
    /// Maps domain entity to persistence entity.
    /// </summary>
    private static LibraryShareLinkEntity MapToPersistence(LibraryShareLink domainEntity)
    {
        ArgumentNullException.ThrowIfNull(domainEntity);

        return new LibraryShareLinkEntity
        {
            Id = domainEntity.Id,
            UserId = domainEntity.UserId,
            ShareToken = domainEntity.ShareToken,
            PrivacyLevel = (int)domainEntity.PrivacyLevel,
            IncludeNotes = domainEntity.IncludeNotes,
            CreatedAt = domainEntity.CreatedAt,
            ExpiresAt = domainEntity.ExpiresAt,
            RevokedAt = domainEntity.RevokedAt,
            ViewCount = domainEntity.ViewCount,
            LastAccessedAt = domainEntity.LastAccessedAt
        };
    }
}

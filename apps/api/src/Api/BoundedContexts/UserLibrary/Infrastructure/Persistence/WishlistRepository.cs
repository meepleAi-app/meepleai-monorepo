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
/// EF Core implementation of WishlistItem repository.
/// Issue #3917: Wishlist Management API.
/// </summary>
internal class WishlistRepository : RepositoryBase, IWishlistRepository
{
    public WishlistRepository(
        MeepleAiDbContext dbContext,
        IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<WishlistItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.WishlistItems
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<WishlistItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.WishlistItems
            .AsNoTracking()
            .OrderByDescending(e => e.Priority)
            .ThenByDescending(e => e.AddedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<WishlistItem?> GetByUserAndGameAsync(
        Guid userId,
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.WishlistItems
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.UserId == userId && e.GameId == gameId, cancellationToken)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<WishlistItem>> GetUserWishlistAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.WishlistItems
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.Priority)
            .ThenByDescending(e => e.AddedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<WishlistItem>> GetHighlightsAsync(
        Guid userId,
        int count = 5,
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.WishlistItems
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.Priority)
            .ThenByDescending(e => e.AddedAt)
            .Take(count)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<bool> IsGameOnWishlistAsync(
        Guid userId,
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        return await DbContext.WishlistItems
            .AsNoTracking()
            .AnyAsync(e => e.UserId == userId && e.GameId == gameId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(WishlistItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        CollectDomainEvents(item);

        var entity = MapToPersistence(item);
        await DbContext.WishlistItems.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(WishlistItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        CollectDomainEvents(item);

        var entity = MapToPersistence(item);
        DbContext.WishlistItems.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(WishlistItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        CollectDomainEvents(item);

        var entity = MapToPersistence(item);
        DbContext.WishlistItems.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.WishlistItems
            .AsNoTracking()
            .AnyAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <remarks>
    /// Passa da <see cref="WishlistItem.Reconstitute"/>, non dalla factory di creazione: quella rende privato ogni
    /// elemento, e la visibilità salvata non tornava (#4552). Ogni colonna scritta da <see cref="MapToPersistence"/>
    /// torna qui (vedi <c>WishlistRepositoryTests</c>).
    /// </remarks>
    private static WishlistItem MapToDomain(WishlistItemEntity entity) =>
        WishlistItem.Reconstitute(
            id: entity.Id,
            userId: entity.UserId,
            gameId: entity.GameId,
            priority: (WishlistPriority)entity.Priority,
            targetPrice: entity.TargetPrice,
            notes: entity.Notes,
            addedAt: entity.AddedAt,
            visibility: (WishlistVisibility)entity.Visibility,
            updatedAt: entity.UpdatedAt);

    private static WishlistItemEntity MapToPersistence(WishlistItem domainEntity)
    {
        return new WishlistItemEntity
        {
            Id = domainEntity.Id,
            UserId = domainEntity.UserId,
            GameId = domainEntity.GameId,
            Priority = (int)domainEntity.Priority,
            TargetPrice = domainEntity.TargetPrice,
            Notes = domainEntity.Notes,
            AddedAt = domainEntity.AddedAt,
            UpdatedAt = domainEntity.UpdatedAt,
            Visibility = (int)domainEntity.Visibility
        };
    }
}

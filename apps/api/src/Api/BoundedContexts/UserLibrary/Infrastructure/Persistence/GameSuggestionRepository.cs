using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserLibrary;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserLibrary.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of GameSuggestion repository.
/// Admin Invitation Flow: manages game suggestions for invited users.
/// </summary>
internal class GameSuggestionRepository : RepositoryBase, IGameSuggestionRepository
{
    public GameSuggestionRepository(
        MeepleAiDbContext dbContext,
        IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<GameSuggestion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.GameSuggestions
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<GameSuggestion>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.GameSuggestions
            .AsNoTracking()
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<GameSuggestion>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.GameSuggestions
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<bool> ExistsForUserAndGameAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default)
    {
        return await DbContext.GameSuggestions
            .AsNoTracking()
            .AnyAsync(e => e.UserId == userId && e.GameId == gameId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(GameSuggestion entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        CollectDomainEvents(entity);

        var persistenceEntity = MapToPersistence(entity);
        await DbContext.GameSuggestions.AddAsync(persistenceEntity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(GameSuggestion entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        CollectDomainEvents(entity);

        var persistenceEntity = MapToPersistence(entity);
        DbContext.GameSuggestions.Update(persistenceEntity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(GameSuggestion entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        CollectDomainEvents(entity);

        var persistenceEntity = MapToPersistence(entity);
        DbContext.GameSuggestions.Remove(persistenceEntity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.GameSuggestions
            .AsNoTracking()
            .AnyAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    private static GameSuggestion MapToDomain(GameSuggestionEntity entity)
    {
        // Use reflection to reconstruct the domain entity from persistence
        // (private constructor + private setters pattern)
        var suggestion = GameSuggestion.Create(
            entity.UserId,
            entity.GameId,
            entity.SuggestedByUserId,
            entity.Source,
            TimeProvider.System);

        // Override Id from database
        var idProp = typeof(GameSuggestion).BaseType?.BaseType?.GetProperty("Id");
        idProp?.SetValue(suggestion, entity.Id);

        // Override CreatedAt from database
        var createdAtProp = typeof(GameSuggestion).GetProperty("CreatedAt");
        createdAtProp?.SetValue(suggestion, entity.CreatedAt);

        // Restore state flags
        if (entity.IsAccepted)
        {
            suggestion.Accept();
        }

        if (entity.IsDismissed)
        {
            suggestion.Dismiss();
        }

        // Clear domain events raised during reconstruction
        suggestion.ClearDomainEvents();

        return suggestion;
    }

    private static GameSuggestionEntity MapToPersistence(GameSuggestion domainEntity)
    {
        return new GameSuggestionEntity
        {
            Id = domainEntity.Id,
            UserId = domainEntity.UserId,
            GameId = domainEntity.GameId,
            SuggestedByUserId = domainEntity.SuggestedByUserId,
            Source = domainEntity.Source,
            CreatedAt = domainEntity.CreatedAt,
            IsDismissed = domainEntity.IsDismissed,
            IsAccepted = domainEntity.IsAccepted
        };
    }
}

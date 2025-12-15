using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of Game repository.
/// Maps between domain Game entity and GameEntity persistence model.
/// </summary>
internal class GameRepository : RepositoryBase, IGameRepository
{
    public GameRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<Game?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var gameEntity = await DbContext.Games
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken).ConfigureAwait(false);

        return gameEntity != null ? MapToDomain(gameEntity) : null;
    }

    public async Task<IReadOnlyList<Game>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var gameEntities = await DbContext.Games
            .AsNoTracking()
            .OrderBy(g => g.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return gameEntities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<Game>> FindByTitleAsync(string titlePattern, CancellationToken cancellationToken = default)
    {
        if (titlePattern is null) throw new ArgumentNullException(nameof(titlePattern));
        var gameEntities = await DbContext.Games
            .AsNoTracking()
            .Where(g => EF.Functions.ILike(g.Name, $"%{titlePattern}%"))
            .OrderBy(g => g.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return gameEntities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(Game game, CancellationToken cancellationToken = default)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));
        // Collect domain events BEFORE mapping to persistence entity
        CollectDomainEvents(game);

        var gameEntity = MapToPersistence(game);
        await DbContext.Games.AddAsync(gameEntity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(Game game, CancellationToken cancellationToken = default)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));
        // Collect domain events BEFORE updating persistence entity
        CollectDomainEvents(game);

        var gameEntity = MapToPersistence(game);
        DbContext.Games.Update(gameEntity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Game game, CancellationToken cancellationToken = default)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));
        var gameEntity = MapToPersistence(game);
        DbContext.Games.Remove(gameEntity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Games.AsNoTracking().AnyAsync(g => g.Id == id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Maps persistence entity to domain entity.
    /// </summary>
    private static Game MapToDomain(Api.Infrastructure.Entities.GameEntity entity)
    {
        var title = new GameTitle(entity.Name);

        Publisher? publisher = entity.Publisher != null ? new Publisher(entity.Publisher) : null;
        YearPublished? yearPublished = entity.YearPublished.HasValue ? new YearPublished(entity.YearPublished.Value) : null;

        PlayerCount? playerCount = null;
        if (entity.MinPlayers.HasValue && entity.MaxPlayers.HasValue)
        {
            playerCount = new PlayerCount(entity.MinPlayers.Value, entity.MaxPlayers.Value);
        }

        PlayTime? playTime = null;
        if (entity.MinPlayTimeMinutes.HasValue && entity.MaxPlayTimeMinutes.HasValue)
        {
            playTime = new PlayTime(entity.MinPlayTimeMinutes.Value, entity.MaxPlayTimeMinutes.Value);
        }

        var game = new Game(
            id: entity.Id,
            title: title,
            publisher: publisher,
            yearPublished: yearPublished,
            playerCount: playerCount,
            playTime: playTime
        );

        // Link BGG data if present
        if (entity.BggId.HasValue)
        {
            game.LinkToBgg(entity.BggId.Value, entity.BggMetadata);
        }

        // Override CreatedAt from DB
        var createdAtProp = typeof(Game).GetProperty("CreatedAt");
        createdAtProp?.SetValue(game, entity.CreatedAt);

        return game;
    }

    /// <summary>
    /// Maps domain entity to persistence entity.
    /// </summary>
    private static Api.Infrastructure.Entities.GameEntity MapToPersistence(Game domainEntity)
    {
        if (domainEntity is null) throw new ArgumentNullException(nameof(domainEntity));
        return new Api.Infrastructure.Entities.GameEntity
        {
            Id = domainEntity.Id,
            Name = domainEntity.Title.Value,
            CreatedAt = domainEntity.CreatedAt,
            Publisher = domainEntity.Publisher?.Name,
            YearPublished = domainEntity.YearPublished?.Value,
            MinPlayers = domainEntity.PlayerCount?.Min,
            MaxPlayers = domainEntity.PlayerCount?.Max,
            MinPlayTimeMinutes = domainEntity.PlayTime?.MinMinutes,
            MaxPlayTimeMinutes = domainEntity.PlayTime?.MaxMinutes,
            BggId = domainEntity.BggId,
            BggMetadata = domainEntity.BggMetadata
        };
    }
}

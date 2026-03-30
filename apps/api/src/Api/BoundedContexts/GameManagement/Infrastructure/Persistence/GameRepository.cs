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
        ArgumentNullException.ThrowIfNull(titlePattern);
        var gameEntities = await DbContext.Games
            .AsNoTracking()
            .Where(g => EF.Functions.ILike(g.Name, $"%{titlePattern}%"))
            .OrderBy(g => g.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return gameEntities.Select(MapToDomain).ToList();
    }

    public async Task<(IReadOnlyList<Game> Games, int Total)> GetPaginatedAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // Validate pagination parameters
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100; // Cap at 100 for performance

        var query = DbContext.Games.AsNoTracking();

        // Apply search filter if provided
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(g => EF.Functions.ILike(g.Name, $"%{search}%"));
        }

        // Get total count before pagination
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        // Apply pagination and ordering
        var gameEntities = await query
            .OrderBy(g => g.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var games = gameEntities.Select(MapToDomain).ToList();

        return (games, total);
    }

    public async Task AddAsync(Game game, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        // Collect domain events BEFORE mapping to persistence entity
        CollectDomainEvents(game);

        var gameEntity = MapToPersistence(game);
        await DbContext.Games.AddAsync(gameEntity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(Game game, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        // Collect domain events BEFORE updating persistence entity
        CollectDomainEvents(game);

        var gameEntity = MapToPersistence(game);
        DbContext.Games.Update(gameEntity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Game game, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        var gameEntity = MapToPersistence(game);
        DbContext.Games.Remove(gameEntity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Games.AsNoTracking().AnyAsync(g => g.Id == id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Maps persistence entity to domain entity (reconstitution — no side effects).
    /// Restores all persisted properties including publication workflow fields (spec-panel C-1).
    /// </summary>
    private static Game MapToDomain(Api.Infrastructure.Entities.GameEntity entity)
    {
        var title = new GameTitle(entity.Name);

        Publisher? publisher = entity.Publisher != null ? new Publisher(entity.Publisher) : null;
        YearPublished? yearPublished = entity.YearPublished.HasValue && entity.YearPublished.Value >= 1000
            ? new YearPublished(entity.YearPublished.Value)
            : null;

        PlayerCount? playerCount = null;
        if (entity.MinPlayers.HasValue && entity.MaxPlayers.HasValue
            && entity.MinPlayers.Value >= 1 && entity.MaxPlayers.Value >= 1)
        {
            playerCount = new PlayerCount(entity.MinPlayers.Value, entity.MaxPlayers.Value);
        }

        PlayTime? playTime = null;
        if (entity.MinPlayTimeMinutes.HasValue && entity.MaxPlayTimeMinutes.HasValue
            && entity.MinPlayTimeMinutes.Value >= 1 && entity.MaxPlayTimeMinutes.Value >= 1)
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

        // Restore properties via reflection (reconstitution — no domain events should fire for these)
        typeof(Game).GetProperty("CreatedAt")!.SetValue(game, entity.CreatedAt);
        typeof(Game).GetProperty("ApprovalStatus")!.SetValue(game, (Domain.ValueObjects.ApprovalStatus)entity.ApprovalStatus);
        typeof(Game).GetProperty("PublishedAt")!.SetValue(game, entity.PublishedAt);
        typeof(Game).GetProperty("SharedGameId")!.SetValue(game, entity.SharedGameId);
        typeof(Game).GetProperty("IconUrl")!.SetValue(game, entity.IconUrl);
        typeof(Game).GetProperty("ImageUrl")!.SetValue(game, entity.ImageUrl);

        if (entity.BggId.HasValue)
        {
            typeof(Game).GetProperty("BggId")!.SetValue(game, entity.BggId);
            typeof(Game).GetProperty("BggMetadata")!.SetValue(game, entity.BggMetadata);
        }

        // Clear domain events raised by the constructor during reconstitution
        game.ClearDomainEvents();

        return game;
    }

    /// <summary>
    /// Maps domain entity to persistence entity.
    /// Note: IsPublished is a DB computed column — do not set it here (spec-panel C-1).
    /// </summary>
    private static Api.Infrastructure.Entities.GameEntity MapToPersistence(Game domainEntity)
    {
        ArgumentNullException.ThrowIfNull(domainEntity);
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
            BggMetadata = domainEntity.BggMetadata,
            SharedGameId = domainEntity.SharedGameId,
            IconUrl = domainEntity.IconUrl,
            ImageUrl = domainEntity.ImageUrl,
            ApprovalStatus = (int)domainEntity.ApprovalStatus,
            // IsPublished is a DB computed column — do not set it here
            PublishedAt = domainEntity.PublishedAt,
        };
    }
}
